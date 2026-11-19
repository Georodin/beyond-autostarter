using System.IO;
using System.Runtime.InteropServices;

namespace BeyondAutoRestart
{
    internal sealed class StartupManager
    {
        private readonly string _startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private string ShortcutPath => Path.Combine(_startupFolder, "BeyondAutoRestart.lnk");

        internal bool IsEnabled => !string.IsNullOrEmpty(_startupFolder) && File.Exists(ShortcutPath);

        internal void SetEnabled(bool enabled)
        {
            if (string.IsNullOrEmpty(_startupFolder))
                throw new DirectoryNotFoundException("Windows did not return a startup folder for this user.");

            if (!enabled)
            {
                File.Delete(ShortcutPath);
                return;
            }

            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Could not locate the running executable.");
            string arguments = "";
            if (string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                string assembly = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
                arguments = $"\"{assembly}\"";
            }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(ShortcutPath);
                try
                {
                    shortcut.TargetPath = executable;
                    shortcut.Arguments = arguments;
                    shortcut.WorkingDirectory = AppContext.BaseDirectory;
                    shortcut.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
