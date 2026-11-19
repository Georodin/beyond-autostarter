using System.Text.Json;
using BeyondAutoRestart.Properties;

namespace BeyondAutoRestart
{
    internal sealed class SettingsManager
    {
        internal const int DefaultSeconds = 5;
        internal const int MaximumSeconds = int.MaxValue / 1000;

        internal int CheckInterval { get; set; } = DefaultSeconds;
        internal int RestartDelay { get; set; } = DefaultSeconds;
        internal bool StopExplorer { get; set; }

        internal IEnumerable<MonitoredProcess> Load()
        {
            var settings = Settings.Default;
            CheckInterval = ValidSeconds(settings.CheckInterval, 1, DefaultSeconds);
            RestartDelay = ValidSeconds(settings.Timeout, 0, DefaultSeconds);
            StopExplorer = settings.KillExplorerEnabled;

            if (!string.IsNullOrWhiteSpace(settings.MonitoredJson))
            {
                var items = JsonSerializer.Deserialize<List<MonitoredProcess?>>(settings.MonitoredJson);
                return items?.OfType<MonitoredProcess>() ?? Enumerable.Empty<MonitoredProcess>();
            }

            if (string.IsNullOrWhiteSpace(settings.SelectedExePath))
                return [];

            return
            [
                new MonitoredProcess
                {
                    Path = settings.SelectedExePath,
                    FirstStartAfterSeconds = RestartDelay,
                    StartIfNotRunning = settings.AutoRestartEnabled
                }
            ];
        }

        internal void Save(IEnumerable<MonitoredProcess> items)
        {
            var settings = Settings.Default;
            settings.CheckInterval = CheckInterval;
            settings.Timeout = RestartDelay;
            settings.KillExplorerEnabled = StopExplorer;
            settings.MonitoredJson = JsonSerializer.Serialize(items);
            settings.Save();
        }

        internal static bool TryReadSeconds(string input, int minimum, out int seconds) =>
            int.TryParse(input, out seconds) && seconds >= minimum && seconds <= MaximumSeconds;

        private static int ValidSeconds(int seconds, int minimum, int fallback) =>
            seconds >= minimum && seconds <= MaximumSeconds ? seconds : fallback;
    }
}
