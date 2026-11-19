using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace BeyondAutoRestart
{
    internal sealed class MonitoringManager : IDisposable
    {
        private readonly MainWindow _window;
        private readonly int _sessionId;
        private readonly Dictionary<MonitoredProcess, (Target Target, CancellationTokenSource Cancellation)> _watchers = [];

        internal MonitoringManager(MainWindow window)
        {
            _window = window;
            using var currentProcess = Process.GetCurrentProcess();
            _sessionId = currentProcess.SessionId;
        }

        internal void Apply(IEnumerable<MonitoredProcess> items)
        {
            var targets = new Dictionary<MonitoredProcess, Target>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in items)
            {
                string? path = NormalizePath(row.Path);
                if (path is null)
                {
                    row.Status = "Select a valid executable";
                    continue;
                }

                if (!paths.Add(path))
                {
                    row.Status = "Duplicate executable";
                    continue;
                }

                int delay = Math.Clamp(row.FirstStartAfterSeconds, 0, SettingsManager.MaximumSeconds);
                targets.Add(row, new Target(path, delay, row.StartIfNotRunning));
            }

            foreach (var (row, watch) in _watchers.ToArray())
            {
                if (!targets.TryGetValue(row, out var target) || watch.Target != target)
                    Remove(row);
            }

            foreach (var (row, target) in targets)
            {
                if (_watchers.ContainsKey(row))
                    continue;

                var cancellation = new CancellationTokenSource();
                _watchers.Add(row, (target, cancellation));
                row.Status = target.InitialDelay > 0 ? "Waiting for first check" : "Checking";
                _ = MonitorAsync(row, target, cancellation.Token);
            }
        }

        internal void Remove(MonitoredProcess row)
        {
            if (_watchers.Remove(row, out var watch))
            {
                watch.Cancellation.Cancel();
                watch.Cancellation.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var row in _watchers.Keys.ToArray())
                Remove(row);
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim();
            if (!Path.IsPathFullyQualified(path) ||
                !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                return null;
            }
        }

        private async Task MonitorAsync(MonitoredProcess row, Target target, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(target.InitialDelay), cancellationToken);

                bool firstCheck = true;
                long? missingSince = null;
                long? lastStartAttempt = null;
                while (true)
                {
                    double nextCheck = _window.CheckInterval;
                    try
                    {
                        var state = await Task.Run(() => CheckApplication(target.Path), cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (state == ApplicationState.Stopped && target.AutoRestart)
                        {
                            missingSince ??= Stopwatch.GetTimestamp();
                            double graceRemaining = (firstCheck ? 0 : _window.RestartDelay) -
                                Stopwatch.GetElapsedTime(missingSince.Value).TotalSeconds;
                            double cooldownRemaining = lastStartAttempt.HasValue
                                ? 10 - Stopwatch.GetElapsedTime(lastStartAttempt.Value).TotalSeconds
                                : 0;
                            double wait = Math.Max(graceRemaining, cooldownRemaining);
                            if (wait <= 0)
                            {
                                lastStartAttempt = Stopwatch.GetTimestamp();
                                missingSince = null;
                                RestartApplication(row, target.Path, cancellationToken);
                            }
                            else
                            {
                                row.Status = "Waiting to restart";
                                nextCheck = Math.Min(nextCheck, wait);
                            }
                        }
                        else
                        {
                            missingSince = null;
                            UpdateApplication(row, state);
                        }
                    }
                    catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        row.Status = $"Check failed: {ex.Message}";
                    }

                    firstCheck = false;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(nextCheck, 0.05)), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The row changed or monitoring stopped.
            }
        }

        private sealed record Target(string Path, int InitialDelay, bool AutoRestart);

        private ApplicationState CheckApplication(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var processes = Process.GetProcessesByName(name);
            bool unverified = false;
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.SessionId != _sessionId || process.HasExited)
                            continue;

                        string? runningPath = process.MainModule?.FileName;
                        if (string.Equals(runningPath, path, StringComparison.OrdinalIgnoreCase))
                            return ApplicationState.Running;

                        unverified |= runningPath is null;
                    }
                    catch (Win32Exception)
                    {
                        // A process may have exited during inspection or be inaccessible.
                        unverified = true;
                    }
                    catch (InvalidOperationException)
                    {
                        // A process may have exited during inspection.
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }

            if (unverified)
                return ApplicationState.Unverified;

            return File.Exists(path) ? ApplicationState.Stopped : ApplicationState.Missing;
        }

        private void UpdateApplication(MonitoredProcess row, ApplicationState state)
        {
            row.Status = state switch
            {
                ApplicationState.Running => "Running",
                ApplicationState.Unverified => "Could not verify process",
                ApplicationState.Missing => "Executable unavailable",
                _ => "Not running"
            };

            if (state == ApplicationState.Running && _window.StopExplorer)
            {
                try
                {
                    StopExplorerShell();
                }
                catch (Win32Exception)
                {
                    row.Status = "Running. Explorer stop failed.";
                }
            }
        }

        private static void RestartApplication(MonitoredProcess row, string path, CancellationToken cancellationToken)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path)!,
                    UseShellExecute = true
                }) ?? throw new InvalidOperationException("Windows did not return a new process.");

                row.Status = "Starting";
                _ = BringToForegroundAsync(process, cancellationToken);
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                row.Status = $"Could not start: {ex.Message}";
            }
        }

        private static async Task BringToForegroundAsync(Process process, CancellationToken cancellationToken)
        {
            using (process)
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    while (!cancellationToken.IsCancellationRequested && timer.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        process.Refresh();
                        if (process.HasExited)
                            return;

                        IntPtr handle = process.MainWindowHandle;
                        if (handle != IntPtr.Zero)
                        {
                            SetForegroundWindow(handle);
                            return;
                        }

                        await Task.Delay(200, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or OperationCanceledException)
                {
                    // Some apps never create a window or exit before it can be focused.
                }
            }
        }

        private void StopExplorerShell()
        {
            IntPtr shellWindow = GetShellWindow();
            if (shellWindow == IntPtr.Zero || GetWindowThreadProcessId(shellWindow, out uint shellId) == 0)
                return;

            Process shell;
            try
            {
                shell = Process.GetProcessById((int)shellId);
            }
            catch (ArgumentException)
            {
                return;
            }

            using (shell)
            {
                if (shell.SessionId != _sessionId ||
                    !string.Equals(shell.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                    return;

                const uint terminateAccess = 0x0001;
                using var handle = OpenProcess(terminateAccess, false, shellId);
                if (handle.IsInvalid || !TerminateProcess(handle, 1))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private enum ApplicationState
        {
            Running,
            Stopped,
            Missing,
            Unverified
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
    }
}
