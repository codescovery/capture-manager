using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace CodescoveryCaptureManager.Domain.Services
{
    /// <summary>Serves the built web UI (read-only, GET/HEAD) from resources embedded in this assembly.</summary>
    internal sealed class StaticSiteResponder
    {
        private const string ResourcePrefix = "web/";
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

        private readonly Assembly _assembly;

        public StaticSiteResponder(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        }

        public bool IsAvailable => Exists(ResourcePrefix + IndexFile);

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

            var resourceName = ResolveResource(request.Url.AbsolutePath);
            if (resourceName == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            await WriteResourceAsync(request, response, resourceName);
        }

        private static bool IsReadMethod(string method)
            => method == "GET" || method == "HEAD";

        private string ResolveResource(string urlPath)
        {
            var relative = Uri.UnescapeDataString(urlPath).TrimStart('/');
            if (!IsSafeRelativePath(relative)) return null;

            var name = ResourcePrefix + (relative.Length == 0 ? IndexFile : relative);
            if (Exists(name)) return name;
            return Path.HasExtension(relative) ? null : ResourcePrefix + IndexFile;
        }

        private static bool IsSafeRelativePath(string relative)
            => relative.IndexOf('\\') < 0 && Array.IndexOf(relative.Split('/'), "..") < 0;

        private bool Exists(string resourceName)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            return stream != null;
        }

        private async Task WriteResourceAsync(HttpListenerRequest request, HttpListenerResponse response, string resourceName)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            response.ContentType = ContentTypes.TryGetValue(Path.GetExtension(resourceName), out var type) ? type : "application/octet-stream";
            response.ContentLength64 = stream.Length;
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["Cache-Control"] = "no-cache";
            if (request.HttpMethod == "HEAD") return;
            await stream.CopyToAsync(response.OutputStream);
        }
    }
}
