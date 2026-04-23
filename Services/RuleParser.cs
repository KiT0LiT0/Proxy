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

                if (IsSectionLabel(line))
                    continue;

                if (line.StartsWith("domain:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("domain:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry
                        {
                            Type = RuleEntryType.DomainSuffix,
                            Value = value,
                            Service = RuleServiceClassifier.Classify(RuleEntryType.DomainSuffix, value),
                            IsEnabled = true
                        });
                    }
                    continue;
                }

                if (line.StartsWith("suffix:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("suffix:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry
                        {
                            Type = RuleEntryType.DomainSuffix,
                            Value = value,
                            Service = RuleServiceClassifier.Classify(RuleEntryType.DomainSuffix, value),
                            IsEnabled = true
                        });
                    }
                    continue;
                }

                if (line.StartsWith("processName:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("processName:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry
                        {
                            Type = RuleEntryType.ProcessName,
                            Value = value,
                            Service = RuleServiceClassifier.Classify(RuleEntryType.ProcessName, value),
                            IsEnabled = true
                        });
                    }
                    continue;
                }

                if (line.StartsWith("process:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("process:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new RuleEntry
                        {
                            Type = RuleEntryType.ProcessName,
                            Value = value,
                            Service = RuleServiceClassifier.Classify(RuleEntryType.ProcessName, value),
                            IsEnabled = true
                        });
                    }
                    continue;
                }

                if (LooksLikeDomain(line))
                {
                    result.Add(new RuleEntry
                    {
                        Type = RuleEntryType.DomainSuffix,
                        Value = line,
                        Service = RuleServiceClassifier.Classify(RuleEntryType.DomainSuffix, line),
                        IsEnabled = true
                    });
                    continue;
                }
            }

            return result;
        }

        private static bool IsSectionLabel(string line)
        {
            if (line.StartsWith("[") && line.EndsWith("]"))
                return true;

            if (line.EndsWith(":") && line.IndexOf(' ') < 0)
            {
                string label = line.Substring(0, line.Length - 1);
                if (!label.Contains("."))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeDomain(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.IndexOf(' ') >= 0)
                return false;

            if (!value.Contains("."))
                return false;

            if (value.Contains(":"))
                return false;

            return true;
        }
    }
}
