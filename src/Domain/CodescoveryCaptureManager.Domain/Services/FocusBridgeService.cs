using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodescoveryCaptureManager.Domain.Interfaces;
using CodescoveryCaptureManager.Domain.Interop;
using CodescoveryCaptureManager.Domain.Structs;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>
    /// Loopback WebSocket server that tells browser pages which picked window is in focus.
    /// Only windows the user picked are ever published, and only to allowed origins.
    /// </summary>
    internal sealed class FocusBridgeService : IFocusBridge
    {
        private const int TitleBufferSize = 1024;

        private readonly FocusBridgeOptions _options;
        private readonly HttpListener _listener = new HttpListener();
        private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new ConcurrentDictionary<Guid, WebSocket>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly string BlurredMessage = JsonSerializer.Serialize(new { focused = false });

        private readonly StaticSiteResponder _site;
        private IntPtr _lastPublishedHandle = IntPtr.Zero;
        private volatile string _lastMessage = BlurredMessage;
        private bool _started;

        public FocusBridgeService(FocusBridgeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _site = new StaticSiteResponder(Path.Combine(AppContext.BaseDirectory, options.WebDirectoryName));
        }

        public void Start()
        {
            if (_started) return;

            try
            {
                _listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/");
                _listener.Start();
                _started = true;
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
            _ = BroadcastAsync(_lastMessage);
        }

        public void PublishBlurred()
        {
            if (!_started || _lastPublishedHandle == IntPtr.Zero) return;

            _lastPublishedHandle = IntPtr.Zero;
            _lastMessage = BlurredMessage;
            _ = BroadcastAsync(_lastMessage);
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
            _clients[clientId] = socket;
            try
            {
                await SendAsync(socket, _lastMessage, cancellationToken);
                await DrainUntilClosedAsync(socket, cancellationToken);
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                socket.Dispose();
            }
        }

        private static async Task DrainUntilClosedAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[256];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
            }
        }

        private async Task BroadcastAsync(string message)
        {
            var openClients = _clients.Values.Where(client => client.State == WebSocketState.Open).ToList();
            foreach (var client in openClients)
                await SendAsync(client, message, _cancellationTokenSource.Token);
        }

        private async Task SendAsync(WebSocket socket, string message, CancellationToken cancellationToken)
        {
            if (message == null || socket.State != WebSocketState.Open) return;

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                var payload = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
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

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            if (_started) _listener.Close();
            _cancellationTokenSource.Dispose();
        }
    }
}
