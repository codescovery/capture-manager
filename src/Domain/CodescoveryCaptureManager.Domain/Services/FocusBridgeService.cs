using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Interop;
using CodescoveryCaptureManager.Domain.Streaming;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>
    /// Loopback WebSocket server for browser pages: serves the web UI, tells which picked window is focused
    /// (text frames) and streams that window as H.264 (binary frames).
    /// Only windows the user picked are ever published, and only to allowed origins.
    /// </summary>
    internal sealed class FocusBridgeService : IFocusBridge
    {
        private const int TitleBufferSize = 1024;
        private const byte VideoFrameType = 1;
        private const byte KeyFrameFlag = 1;

        private static readonly string BlurredMessage = JsonSerializer.Serialize(new { focused = false });

        private readonly FocusBridgeOptions _options;
        private readonly IVideoStream _videoStream;
        private readonly StaticSiteResponder _site;
        private readonly HttpListener _listener = new HttpListener();
        private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new ConcurrentDictionary<Guid, ClientConnection>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private IntPtr _lastPublishedHandle = IntPtr.Zero;
        private volatile string _lastMessage = BlurredMessage;
        private bool _started;

        public FocusBridgeService(FocusBridgeOptions options, IVideoStream videoStream)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _videoStream = videoStream ?? throw new ArgumentNullException(nameof(videoStream));
            _site = new StaticSiteResponder(typeof(FocusBridgeService).Assembly);
        }

        public void Start()
        {
            if (_started) return;

            try
            {
                _listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");
                _listener.Start();
                _started = true;
                _videoStream.FrameEncoded += OnFrameEncoded;
                _ = AcceptLoopAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Focus bridge not started. {ex.Message}");
            }
        }

        public void Publish(CapturableWindow focusedWindow)
        {
            if (!_started || focusedWindow.Handle == _lastPublishedHandle) return;

            _lastPublishedHandle = focusedWindow.Handle;
            _lastMessage = BuildMessage(focusedWindow);
            _videoStream.RequestKeyFrame();
            _ = BroadcastTextAsync(_lastMessage);
        }

        public void PublishBlurred()
        {
            if (!_started || _lastPublishedHandle == IntPtr.Zero) return;

            _lastPublishedHandle = IntPtr.Zero;
            _lastMessage = BlurredMessage;
            _ = BroadcastTextAsync(_lastMessage);
        }

        private static string BuildMessage(CapturableWindow window)
        {
            var title = new StringBuilder(TitleBufferSize);
            NativeMethods.GetWindowText(window.Handle, title, title.Capacity);
            return JsonSerializer.Serialize(new
            {
                focused = true,
                focusedTitle = title.ToString(),
                processName = ReadProcessName(window),
                handle = window.Handle.ToInt64(),
            });
        }

        private static string ReadProcessName(CapturableWindow window)
        {
            try
            {
                return window.Process?.ProcessName;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await AcceptContextAsync();
                if (context == null) return;
                _ = HandleAsync(context, cancellationToken);
            }
        }

        private async Task<HttpListenerContext> AcceptContextAsync()
        {
            try
            {
                return await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                return null;
            }
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                await ServeSiteAsync(context);
                return;
            }

            if (!await RejectIfNotAllowedAsync(context)) return;

            try
            {
                var webSocketContext = await context.AcceptWebSocketAsync(null);
                await ServeClientAsync(webSocketContext.WebSocket, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Focus bridge client failed. {ex.Message}");
            }
        }

        private async Task<bool> RejectIfNotAllowedAsync(HttpListenerContext context)
        {
            var statusCode = ResolveRejection(context.Request);
            if (statusCode == 0) return true;

            context.Response.StatusCode = statusCode;
            context.Response.Close();
            await Task.CompletedTask;
            return false;
        }

        private async Task ServeSiteAsync(HttpListenerContext context)
        {
            try
            {
                if (_site.IsAvailable)
                {
                    await _site.RespondAsync(context);
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.UpgradeRequired;
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Focus bridge site failed. {ex.Message}");
            }
        }

        private int ResolveRejection(HttpListenerRequest request)
        {
            if (!IsAllowedOrigin(request.Headers["Origin"])) return (int)HttpStatusCode.Forbidden;
            return 0;
        }

        private bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            if (uri.IsLoopback) return true;
            return _options.AllowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
        }

        private async Task ServeClientAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var clientId = Guid.NewGuid();
            var client = new ClientConnection(socket);
            _clients[clientId] = client;
            _videoStream.SetViewerCount(_clients.Count);
            try
            {
                await client.SendTextAsync(_lastMessage, cancellationToken);
                _videoStream.RequestKeyFrame();
                await DrainUntilClosedAsync(socket, cancellationToken);
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                _videoStream.SetViewerCount(_clients.Count);
                client.Dispose();
            }
        }

        // The only message a page may send: "key" = I lost frames, send a key frame.
        private async Task DrainUntilClosedAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[256];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (IsKeyFrameRequest(buffer, result)) _videoStream.RequestKeyFrame();
            }
        }

        private static bool IsKeyFrameRequest(byte[] buffer, WebSocketReceiveResult result)
            => result.MessageType == WebSocketMessageType.Text && result.Count == 3 && Encoding.ASCII.GetString(buffer, 0, 3) == "key";

        private async Task BroadcastTextAsync(string message)
        {
            var openClients = _clients.Values.Where(client => client.IsOpen).ToList();
            foreach (var client in openClients)
                await client.SendTextAsync(message, _cancellationTokenSource.Token);
        }

        private void OnFrameEncoded(EncodedFrame frame)
        {
            var payload = BuildVideoPayload(frame);
            foreach (var client in _clients.Values)
                if (!client.TrySendVideo(payload, frame.IsKeyFrame, _cancellationTokenSource.Token))
                    _videoStream.RequestKeyFrame();
        }

        private static byte[] BuildVideoPayload(EncodedFrame frame)
        {
            var payload = new byte[frame.Data.Length + 2];
            payload[0] = VideoFrameType;
            payload[1] = frame.IsKeyFrame ? KeyFrameFlag : (byte)0;
            Buffer.BlockCopy(frame.Data, 0, payload, 2, frame.Data.Length);
            return payload;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _videoStream.FrameEncoded -= OnFrameEncoded;
            if (_started) _listener.Close();
            foreach (var client in _clients.Values) client.Dispose();
            _cancellationTokenSource.Dispose();
        }

        /// <summary>One browser connection. Text messages are queued in order; video drops frames instead of queueing.</summary>
        private sealed class ClientConnection : IDisposable
        {
            private readonly WebSocket _socket;
            private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
            private volatile bool _needsKeyFrame = true;

            public ClientConnection(WebSocket socket)
            {
                _socket = socket;
            }

            public bool IsOpen => _socket.State == WebSocketState.Open;

            public async Task SendTextAsync(string message, CancellationToken cancellationToken)
            {
                if (message == null || !IsOpen) return;

                await _sendLock.WaitAsync(cancellationToken);
                try
                {
                    var payload = Encoding.UTF8.GetBytes(message);
                    await _socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Focus bridge send failed. {ex.Message}");
                }
                finally
                {
                    _sendLock.Release();
                }
            }

            /// <returns>False when this client lost frames and needs a new key frame.</returns>
            public bool TrySendVideo(byte[] payload, bool isKeyFrame, CancellationToken cancellationToken)
            {
                if (!IsOpen) return true;
                if (_needsKeyFrame && !isKeyFrame) return false;
                if (!_sendLock.Wait(0))
                {
                    _needsKeyFrame = true;
                    return false;
                }

                _needsKeyFrame = false;
                _ = SendVideoAsync(payload, cancellationToken);
                return true;
            }

            private async Task SendVideoAsync(byte[] payload, CancellationToken cancellationToken)
            {
                try
                {
                    await _socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, cancellationToken);
                }
                catch (Exception)
                {
                    _needsKeyFrame = true;
                }
                finally
                {
                    _sendLock.Release();
                }
            }

            public void Dispose()
            {
                _socket.Dispose();
            }
        }
    }
}
