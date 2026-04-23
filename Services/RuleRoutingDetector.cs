using System;
using System.IO;
using ProxyShellReady.Models;

namespace ProxyShellReady.Services
{
    public static class RuleRoutingDetector
    {
        public static RuleRoutingMode Detect(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string normalized = name.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

            if (normalized.Contains("direct"))
                return RuleRoutingMode.Direct;

            if (normalized.Contains("block") || normalized.Contains("reject"))
                return RuleRoutingMode.Block;

            return RuleRoutingMode.Proxy;
        }
    }
}
