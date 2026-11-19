using System.Threading;

namespace BeyondAutoRestart
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _instance;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);
            _instance = new Mutex(false, @"Local\BeyondAutoRestart", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                Shutdown();
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Start();
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            _instance?.Dispose();
            base.OnExit(e);
        }
    }
}
