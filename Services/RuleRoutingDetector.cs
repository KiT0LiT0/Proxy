using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static RuleRoutingMode Detect(string filePath, IReadOnlyCollection<RuleEntry> entries)
        {
            RuleRoutingMode byName = Detect(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string normalizedName = fileName.ToLowerInvariant();
            if (normalizedName.Contains("direct") || normalizedName.Contains("proxy") || normalizedName.Contains("block") || normalizedName.Contains("reject"))
                return byName;

            int directScore = 0;
            int proxyScore = 0;
            int blockScore = 0;

            foreach (RuleEntry entry in entries ?? Enumerable.Empty<RuleEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    continue;

                string value = entry.Value.Trim().ToLowerInvariant();

                if (entry.Type == RuleEntryType.Domain || entry.Type == RuleEntryType.DomainSuffix)
                {
                    if (value.EndsWith(".ru") || value.EndsWith(".su") || value.EndsWith(".рф"))
                        directScore += 2;

                    if (ContainsAny(value, "ads.", "adservice", "doubleclick", "googlesyndication", "googlead", "analytics", "tracking", "metric"))
                        blockScore += 3;

                    if (ContainsAny(value, "youtube", "googlevideo", "discord", "steam", "origin", "ea.com", "ubisoft", "twitch", "openai", "cloudfront", "amazonaws"))
                        proxyScore += 2;
                }
                else if (entry.Type == RuleEntryType.ProcessName)
                {
                    if (ContainsAny(value, "chrome.exe", "firefox.exe", "msedge.exe", "opera.exe", "discord.exe", "telegram.exe", "steam.exe"))
                        proxyScore += 2;
                }
            }

            if (blockScore > directScore && blockScore > proxyScore)
                return RuleRoutingMode.Block;

            if (directScore > proxyScore)
                return RuleRoutingMode.Direct;

            if (proxyScore > 0)
                return RuleRoutingMode.Proxy;

            return byName;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            foreach (string part in parts)
            {
                if (value.Contains(part))
                    return true;
            }
            return false;
        }
    }
}
