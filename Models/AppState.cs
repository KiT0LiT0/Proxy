using System.Collections.Generic;

namespace ProxyShellReady.Models
{
    public class AppState
    {
        public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.WholeComputer;
        public AppThemeMode Theme { get; set; } = AppThemeMode.Dark;
        public LocalProxyMode LocalMode { get; set; } = LocalProxyMode.MixedSystemProxy;
        public string SingBoxPath { get; set; } = string.Empty;
        public string SocksHost { get; set; } = "127.0.0.1";
        public int SocksPort { get; set; } = 1080;
        public string SocksUsername { get; set; } = string.Empty;
        public string SocksPassword { get; set; } = string.Empty;
        public List<AppEntry> SelectedApps { get; set; } = new List<AppEntry>();
        public List<RuleFileItem> RuleFiles { get; set; } = new List<RuleFileItem>();
    }
}
