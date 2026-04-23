using System;
using System.Collections.Generic;
using System.IO;
using ProxyShellReady.Models;

namespace ProxyShellReady.Services
{
    public static class RuleParser
    {
        public static List<RuleEntry> ParseFile(string path)
        {
            List<RuleEntry> result = new List<RuleEntry>();

            if (!File.Exists(path))
                return result;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = (rawLine ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                if (line.StartsWith("domain:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("domain:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry { Type = RuleEntryType.DomainSuffix, Value = value });
                    }
                    continue;
                }

                if (line.StartsWith("suffix:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("suffix:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry { Type = RuleEntryType.DomainSuffix, Value = value });
                    }
                    continue;
                }

                if (line.StartsWith("processName:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("processName:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry { Type = RuleEntryType.ProcessName, Value = value });
                    }
                    continue;
                }

                if (line.StartsWith("process:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("process:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry { Type = RuleEntryType.ProcessName, Value = value });
                    }
                    continue;
                }
            }

            return result;
        }
    }
}
