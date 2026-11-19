using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace BeyondAutoRestart
{
    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settings = new();
        private readonly StartupManager _startup = new();
        private readonly MonitoringManager _monitoring;
        private readonly TrayIconManager _tray;

        public MainWindow()
        {
            InitializeComponent();
            ItemsGrid.ItemsSource = Items;
            _monitoring = new MonitoringManager(this);
            _tray = new TrayIconManager(this);
        }

        internal ObservableCollection<MonitoredProcess> Items { get; } = [];
        internal int CheckInterval => _settings.CheckInterval;
        internal int RestartDelay => _settings.RestartDelay;
        internal bool StopExplorer => _settings.StopExplorer;

        internal void Start()
        {
            try
            {
                foreach (var item in _settings.Load())
                    Items.Add(item);
            }
            catch (Exception ex) when (ex is ConfigurationErrorsException or IOException or UnauthorizedAccessException or JsonException)
            {
                ShowError("Could not load settings", ex.Message);
            }

            IntervalTextBox.Text = CheckInterval.ToString();
            TimeoutTextBox.Text = RestartDelay.ToString();
            KillExplorerCheckBox.IsChecked = StopExplorer;
            BootCheckBox.IsChecked = _startup.IsEnabled;

            _monitoring.Apply(Items);
            if (Items.Any(item => !string.IsNullOrWhiteSpace(item.Path)))
                MinimizeToTray();
            else
                ShowMainWindow();
        }

        internal void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _tray.Visible = false;
        }

        private void MinimizeToTray()
        {
            Hide();
            _tray.Visible = true;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
                MinimizeToTray();
        }

        protected override void OnClosed(EventArgs e)
        {
            _monitoring.Dispose();
            _tray.Dispose();
            base.OnClosed(e);
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var row = new MonitoredProcess();
            Items.Add(row);
            ItemsGrid.SelectedItem = row;
            ItemsGrid.ScrollIntoView(row);
            FeedbackText.Text = "";
        }

        private void BrowseRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MonitoredProcess row)
                return;

            if (!ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true))
                return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Windows executables (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            _monitoring.Remove(row);
            row.Path = dialog.FileName;
            row.Status = "Save to apply";
            FeedbackText.Text = "";
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is MonitoredProcess row)
            {
                _monitoring.Remove(row);
                Items.Remove(row);
                FeedbackText.Text = "";
            }
        }

        private void BootCheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _startup.SetEnabled(BootCheckBox.IsChecked == true);
            }
            catch (Exception ex)
            {
                BootCheckBox.IsChecked = _startup.IsEnabled;
                ShowError("Could not update Windows startup", ex.Message);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                ShowError("Check the application list", "Enter the first-start delay in whole seconds.");
                return;
            }

            if (!SettingsManager.TryReadSeconds(IntervalTextBox.Text, 1, out int interval) ||
                !SettingsManager.TryReadSeconds(TimeoutTextBox.Text, 0, out int timeout))
            {
                ShowError("Check the timing settings", "Enter a positive check interval and a nonnegative restart delay.");
                return;
            }

            if (Items.Any(item => item.FirstStartAfterSeconds < 0 || item.FirstStartAfterSeconds > SettingsManager.MaximumSeconds))
            {
                ShowError("Check the application list", "Enter a first-start delay from 0 to 2,147,483 seconds.");
                return;
            }

            _settings.CheckInterval = interval;
            _settings.RestartDelay = timeout;
            _settings.StopExplorer = KillExplorerCheckBox.IsChecked == true;

            try
            {
                _settings.Save(Items);
                _monitoring.Apply(Items);
                FeedbackText.Text = "Settings saved";
            }
            catch (Exception ex) when (ex is ConfigurationErrorsException or IOException or UnauthorizedAccessException)
            {
                ShowError("Could not save settings", ex.Message);
            }
        }

        private void ShowError(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
