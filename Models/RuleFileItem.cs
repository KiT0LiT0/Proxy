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

        [JsonIgnore]
        public List<RuleEntry> Entries { get; set; } = new List<RuleEntry>();

        [JsonIgnore]
        public string DisplaySubtitle
        {
            get { return EntryCount + " правил • " + FullPath; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
