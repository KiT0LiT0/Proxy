using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProxyShellReady.Models
{
    public class AppEntry : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isEnabled = true;

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(); }
        }

        public string FullPath
        {
            get { return _fullPath; }
            set { _fullPath = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
