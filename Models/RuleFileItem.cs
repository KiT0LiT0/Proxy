using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ProxyShellReady.Models
{
    public class RuleFileItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isEnabled = true;
        private int _entryCount;
        private RuleRoutingMode _routingMode = RuleRoutingMode.Proxy;
        private string _serviceSummary = string.Empty;

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(); }
        }

        public string FullPath
        {
            get { return _fullPath; }
            set { _fullPath = value; OnPropertyChanged(); OnPropertyChanged("DisplaySubtitle"); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public int EntryCount
        {
            get { return _entryCount; }
            set { _entryCount = value; OnPropertyChanged(); OnPropertyChanged("DisplaySubtitle"); }
        }

        public RuleRoutingMode RoutingMode
        {
            get { return _routingMode; }
            set { _routingMode = value; OnPropertyChanged(); OnPropertyChanged("DisplaySubtitle"); }
        }

        public string ServiceSummary
        {
            get { return _serviceSummary; }
            set { _serviceSummary = value; OnPropertyChanged(); OnPropertyChanged("DisplaySubtitle"); }
        }

        public List<RuleEntry> Entries { get; set; } = new List<RuleEntry>();

        [JsonIgnore]
        public string DisplaySubtitle
        {
            get
            {
                string services = string.IsNullOrWhiteSpace(ServiceSummary) ? "Services: n/a" : ServiceSummary;
                return EntryCount + " правил • " + RoutingMode + " • " + services + " • " + FullPath;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
