using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProxyShellReady.Models;

namespace ProxyShellReady.Services
{
    public static class SingBoxConfigBuilder
    {
        public static string Build(AppState state, IReadOnlyCollection<RuleFileItem> ruleFiles, IReadOnlyCollection<AppEntry> selectedApps)
        {
            List<string> domainSuffixes = new List<string>();
            List<string> processNames = new List<string>();

            foreach (RuleFileItem file in ruleFiles.Where(r => r.IsEnabled))
            {
                foreach (RuleEntry entry in file.Entries)
                {
                    if (entry.Type == RuleEntryType.Domain || entry.Type == RuleEntryType.DomainSuffix)
                        domainSuffixes.Add(entry.Value);
                    else if (entry.Type == RuleEntryType.ProcessName)
                        processNames.Add(Path.GetFileName(entry.Value));
                }
            }

            if (state.ConnectionMode == ConnectionMode.AppSelection)
            {
                foreach (AppEntry app in selectedApps.Where(a => a.IsEnabled))
                {
                    string name = Path.GetFileName(app.FullPath);
                    if (!string.IsNullOrWhiteSpace(name))
                        processNames.Add(name);
                }
            }

            domainSuffixes = domainSuffixes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            processNames = processNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<Dictionary<string, object>> rules = new List<Dictionary<string, object>>();

            rules.Add(new Dictionary<string, object>
            {
                ["ip_cidr"] = new[]
                {
                    "127.0.0.0/8",
                    "10.0.0.0/8",
                    "172.16.0.0/12",
                    "192.168.0.0/16",
                    "169.254.0.0/16"
                },
                ["outbound"] = "direct"
            });

            rules.Add(new Dictionary<string, object>
            {
                ["process_name"] = new[] { "sing-box.exe", "ProxyShellReady.exe" },
                ["outbound"] = "direct"
            });

            if (domainSuffixes.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = domainSuffixes.ToArray(),
                    ["outbound"] = "main"
                });
            }

            if (processNames.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["process_name"] = processNames.ToArray(),
                    ["outbound"] = "main"
                });
            }

            bool hasSpecificProxyRules = domainSuffixes.Count > 0 || processNames.Count > 0;

            Dictionary<string, object> route = new Dictionary<string, object>
            {
                ["auto_detect_interface"] = true,
                ["rules"] = rules.ToArray(),
                ["final"] = (state.ConnectionMode == ConnectionMode.WholeComputer && !hasSpecificProxyRules) ? "main" : "direct"
            };

            Dictionary<string, object> root = new Dictionary<string, object>
            {
                ["log"] = new Dictionary<string, object>
                {
                    ["level"] = "info",
                    ["timestamp"] = true
                },
                ["dns"] = new Dictionary<string, object>
                {
                    ["servers"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "local",
                            ["tag"] = "local"
                        }
                    }
                },
                ["inbounds"] = BuildInbounds(state.LocalMode),
                ["outbounds"] = new object[]
                {
                    BuildMainOutbound(state),
                    new Dictionary<string, object>
                    {
                        ["type"] = "direct",
                        ["tag"] = "direct"
                    }
                },
                ["route"] = route
            };

            return JsonSerializer.Serialize(root, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static object[] BuildInbounds(LocalProxyMode mode)
        {
            if (mode == LocalProxyMode.Tun)
            {
                return new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "tun",
                        ["tag"] = "tun-in",
                        ["interface_name"] = "ProxyShellTun",
                        ["address"] = new[] { "172.19.0.1/30" },
                        ["mtu"] = 1400,
                        ["auto_route"] = true,
                        ["strict_route"] = false,
                        ["stack"] = "system"
                    }
                };
            }

            return new object[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "mixed",
                    ["tag"] = "mixed-in",
                    ["listen"] = "127.0.0.1",
                    ["listen_port"] = 2080,
                    ["set_system_proxy"] = true
                }
            };
        }

        private static Dictionary<string, object> BuildMainOutbound(AppState state)
        {
            Dictionary<string, object> outbound = new Dictionary<string, object>
            {
                ["type"] = "socks",
                ["tag"] = "main",
                ["server"] = state.SocksHost,
                ["server_port"] = state.SocksPort,
                ["version"] = "5"
            };

            if (!string.IsNullOrWhiteSpace(state.SocksUsername))
                outbound["username"] = state.SocksUsername;

            if (!string.IsNullOrWhiteSpace(state.SocksPassword))
                outbound["password"] = state.SocksPassword;

            return outbound;
        }
    }
}
