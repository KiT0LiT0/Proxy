using System;
using ProxyShellReady.Models;

namespace ProxyShellReady.Services
{
    public static class RuleServiceClassifier
    {
        public static string Classify(RuleEntryType type, string value)
        {
            string text = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
                return "Unknown";

            if (ContainsAny(text, "ozon"))
                return "Ozon";
            if (ContainsAny(text, "yandex", "ya.ru"))
                return "Yandex";
            if (ContainsAny(text, "vk.com", "vkuservideo", "vkuseraudio", "mycdn.me"))
                return "VK";
            if (ContainsAny(text, "sber", "alfabank", "tinkoff", "vtb", "gazprombank"))
                return "Banks";
            if (ContainsAny(text, "googleads", "doubleclick", "googlesyndication", "adservice", "app-measurement"))
                return "Ads";
            if (ContainsAny(text, "youtube", "googlevideo"))
                return "YouTube";
            if (ContainsAny(text, "google", "gstatic"))
                return "Google";
            if (ContainsAny(text, "amazonaws", "cloudfront", "amazon"))
                return "Amazon";
            if (ContainsAny(text, "discord"))
                return "Discord";
            if (ContainsAny(text, "telegram"))
                return "Telegram";
            if (ContainsAny(text, "steam", "origin", "ea.com", "ubisoft", "riot", "battle.net"))
                return "Games";
            if (type == RuleEntryType.Domain || type == RuleEntryType.DomainSuffix)
            {
                if (text.EndsWith(".ru") || text.EndsWith(".su") || text.EndsWith(".рф"))
                    return "RU Services";
            }

            return "Other";
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
