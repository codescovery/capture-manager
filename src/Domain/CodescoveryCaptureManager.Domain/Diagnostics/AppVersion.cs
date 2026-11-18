using System.Reflection;

namespace CodescoveryCaptureManager.Domain.Diagnostics
{
    /// <summary>Version of the running app (set by the release build), without build metadata. "dev" for a plain build.</summary>
    public static class AppVersion
    {
        public const string Unknown = "dev";

        public static string Current { get; } = Read(Assembly.GetEntryAssembly());

        internal static string Read(Assembly assembly)
            => Trim(assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        /// <summary>"1.4.0+3f2a9c1" -> "1.4.0".</summary>
        internal static string Trim(string informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion)) return Unknown;
            var buildMetadata = informationalVersion.IndexOf('+');
            return buildMetadata < 0 ? informationalVersion : informationalVersion.Substring(0, buildMetadata);
        }
    }
}
