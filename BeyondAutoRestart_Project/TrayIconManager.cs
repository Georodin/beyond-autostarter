using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace BeyondAutoRestart
{
    internal sealed class TrayIconManager : IDisposable
    {
        private readonly Icon _icon;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;

        internal TrayIconManager(MainWindow window)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BeyondAutoRestart.icon.ico")
                ?? throw new FileNotFoundException("The tray icon resource is missing.");
            using var icon = new Icon(stream);
            _icon = (Icon)icon.Clone();

            _menu = new ContextMenuStrip();
            _menu.Items.Add("Open settings", null, (_, _) => window.ShowMainWindow());
            _menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());

            _notifyIcon = new NotifyIcon
            {
                Icon = _icon,
                Text = "Beyond Autostarter",
                ContextMenuStrip = _menu
            };
            _notifyIcon.DoubleClick += (_, _) => window.ShowMainWindow();
        }

        internal bool Visible
        {
            set => _notifyIcon.Visible = value;
        }

        public void Dispose()
        {
            _notifyIcon.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }
    }
}
