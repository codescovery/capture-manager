using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>Serves the built web UI (read-only, GET/HEAD) from a directory next to the executable.</summary>
    internal sealed class StaticSiteResponder
    {
        private const string IndexFile = "index.html";

        private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".ico"] = "image/x-icon",
            [".woff2"] = "font/woff2",
            [".map"] = "application/json; charset=utf-8",
        };

        private readonly string _rootDirectory;

        public StaticSiteResponder(string rootDirectory)
        {
            _rootDirectory = Path.GetFullPath(rootDirectory);
        }

        public bool IsAvailable => File.Exists(Path.Combine(_rootDirectory, IndexFile));

        public async Task RespondAsync(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                await WriteAsync(context.Request, response);
            }
            finally
            {
                response.Close();
            }
        }

        private async Task WriteAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsReadMethod(request.HttpMethod))
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var filePath = ResolveFile(request.Url.AbsolutePath);
            if (filePath == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            await WriteFileAsync(request, response, filePath);
        }

        private static bool IsReadMethod(string method)
            => method == "GET" || method == "HEAD";

        private string ResolveFile(string urlPath)
        {
            var relative = Uri.UnescapeDataString(urlPath).TrimStart('/');
            var candidate = Path.GetFullPath(Path.Combine(_rootDirectory, relative.Length == 0 ? IndexFile : relative));
            if (!IsInsideRoot(candidate)) return null;
            if (File.Exists(candidate)) return candidate;
            return Path.HasExtension(candidate) ? null : Path.Combine(_rootDirectory, IndexFile);
        }

        private bool IsInsideRoot(string fullPath)
            => fullPath.StartsWith(_rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static async Task WriteFileAsync(HttpListenerRequest request, HttpListenerResponse response, string filePath)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            response.ContentType = ContentTypes.TryGetValue(Path.GetExtension(filePath), out var type) ? type : "application/octet-stream";
            response.ContentLength64 = bytes.Length;
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["Cache-Control"] = "no-cache";
            if (request.HttpMethod == "HEAD") return;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
