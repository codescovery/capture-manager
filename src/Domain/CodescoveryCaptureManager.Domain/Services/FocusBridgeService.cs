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
        private IntPtr _lastPublishedHandle = IntPtr.Zero;
        private string _lastMessage;
        private bool _started;

        public FocusBridgeService(FocusBridgeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
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

        private static string BuildMessage(CapturableWindow window)
        {
            var title = new StringBuilder(TitleBufferSize);
            NativeMethods.GetWindowText(window.Handle, title, title.Capacity);
            return JsonSerializer.Serialize(new { focusedTitle = title.ToString(), processName = ReadProcessName(window) });
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

        private int ResolveRejection(HttpListenerRequest request)
        {
            if (!request.IsWebSocketRequest) return (int)HttpStatusCode.UpgradeRequired;
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
