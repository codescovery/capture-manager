using System;
using System.Collections.Generic;
using System.Linq;

namespace CodescoveryCaptureManager.Domain.Services
{
    public sealed class FocusBridgeOptions
    {
        public const string AllowedOriginsVariable = "CAPTURE_MANAGER_ALLOWED_ORIGINS";

        public int Port { get; set; } = 47800;

        public IReadOnlyList<string> AllowedOrigins { get; set; } = Array.Empty<string>();

        public static FocusBridgeOptions FromEnvironment()
        {
            var configured = Environment.GetEnvironmentVariable(AllowedOriginsVariable) ?? string.Empty;
            var origins = configured
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            return new FocusBridgeOptions { AllowedOrigins = origins };
        }
    }
}
