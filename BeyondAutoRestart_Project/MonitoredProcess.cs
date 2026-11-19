using System.ComponentModel;
using System.Text.Json.Serialization;

namespace BeyondAutoRestart
{
    public sealed class MonitoredProcess : INotifyPropertyChanged
    {
        private string _path = "";
        private string _status = "Select an executable";

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Path)));
            }
        }

        public int FirstStartAfterSeconds { get; set; }
        public bool StartIfNotRunning { get; set; } = true;

        [JsonIgnore]
        public string Status
        {
            get => _status;
            internal set
            {
                if (_status == value)
                    return;

                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
