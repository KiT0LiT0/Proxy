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
            List<string> proxyDomainSuffixes = new List<string>();
            List<string> directDomainSuffixes = new List<string>();
            List<string> blockDomainSuffixes = new List<string>();

            List<string> proxyProcessNames = new List<string>();
            List<string> directProcessNames = new List<string>();
            List<string> blockProcessNames = new List<string>();

            foreach (RuleFileItem file in ruleFiles.Where(r => r.IsEnabled))
            {
                foreach (RuleEntry entry in file.Entries)
                {
                    if (entry.Type == RuleEntryType.Domain || entry.Type == RuleEntryType.DomainSuffix)
                        AddByMode(file.RoutingMode, entry.Value, proxyDomainSuffixes, directDomainSuffixes, blockDomainSuffixes);
                    else if (entry.Type == RuleEntryType.ProcessName)
                        AddByMode(file.RoutingMode, Path.GetFileName(entry.Value), proxyProcessNames, directProcessNames, blockProcessNames);
                }
            }

            if (state.ConnectionMode == ConnectionMode.AppSelection)
            {
                foreach (AppEntry app in selectedApps.Where(a => a.IsEnabled))
                {
                    string name = Path.GetFileName(app.FullPath);
                    if (!string.IsNullOrWhiteSpace(name))
                        proxyProcessNames.Add(name);
                }
            }

            proxyDomainSuffixes = Normalize(proxyDomainSuffixes);
            directDomainSuffixes = Normalize(directDomainSuffixes);
            blockDomainSuffixes = Normalize(blockDomainSuffixes);

            proxyProcessNames = Normalize(proxyProcessNames);
            directProcessNames = Normalize(directProcessNames);
            blockProcessNames = Normalize(blockProcessNames);

            List<Dictionary<string, object>> rules = new List<Dictionary<string, object>>();

            if (blockDomainSuffixes.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = blockDomainSuffixes.ToArray(),
                    ["outbound"] = "block"
                });
            }

            if (blockProcessNames.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["process_name"] = blockProcessNames.ToArray(),
                    ["outbound"] = "block"
                });
            }

            if (directDomainSuffixes.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = directDomainSuffixes.ToArray(),
                    ["outbound"] = "direct"
                });
            }

            if (directProcessNames.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["process_name"] = directProcessNames.ToArray(),
                    ["outbound"] = "direct"
                });
            }

            if (proxyDomainSuffixes.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = proxyDomainSuffixes.ToArray(),
                    ["outbound"] = "main"
                });
            }

            if (proxyProcessNames.Count > 0)
            {
                rules.Add(new Dictionary<string, object>
                {
                    ["process_name"] = proxyProcessNames.ToArray(),
                    ["outbound"] = "main"
                });
            }

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

            Dictionary<string, object> route = new Dictionary<string, object>
            {
                ["auto_detect_interface"] = true,
                ["rules"] = rules.ToArray(),
                ["final"] = state.ConnectionMode == ConnectionMode.WholeComputer ? "main" : "direct"
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
                ["inbounds"] = BuildInbounds(state.LocalMode, state.ConnectionMode),
                ["outbounds"] = new object[]
                {
                    BuildMainOutbound(state),
                    new Dictionary<string, object>
                    {
                        ["type"] = "direct",
                        ["tag"] = "direct"
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "block",
                        ["tag"] = "block"
                    }
                },
                ["route"] = route
            };

            return JsonSerializer.Serialize(root, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static void AddByMode(
            RuleRoutingMode mode,
            string value,
            List<string> proxyList,
            List<string> directList,
            List<string> blockList)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (mode == RuleRoutingMode.Direct)
            {
                directList.Add(value);
                return;
            }

            if (mode == RuleRoutingMode.Block)
            {
                blockList.Add(value);
                return;
            }

            proxyList.Add(value);
        }

        private static List<string> Normalize(List<string> values)
        {
            return values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static object[] BuildInbounds(LocalProxyMode mode, ConnectionMode connectionMode)
        {
            if (mode == LocalProxyMode.Tun || connectionMode == ConnectionMode.AppSelection)
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
                    ["set_system_proxy"] = connectionMode == ConnectionMode.WholeComputer
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
