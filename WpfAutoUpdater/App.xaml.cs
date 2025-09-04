using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfAutoUpdater.ViewModels;

namespace WpfAutoUpdater
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) If we are in helper mode (apply update)
            if (HasArg(e.Args, "--apply-update"))
            {
                await ApplyUpdateAndRelaunchAsync();
                Shutdown();
                return;
            }

            // 2) Normal startup
            bool skipUpdateCheck = HasArg(e.Args, "--skip-update-check");

            var vm = new MainViewModel();

            if (skipUpdateCheck)
            {
                // Update was just applied → go directly to View
                ShowView(vm);
                return;
            }

            try
            {
                await vm.CheckForUpdateAsync();

                if (vm.IsUpdateAvailable)
                {
                    // Show update UI
                    var updateWindow = new MainWindow { DataContext = vm };
                    MainWindow = updateWindow;
                    updateWindow.Show();
                    return;
                }
            }
            catch
            {
                // ignore errors → continue to View
            }

            // 3) No update → show View
            ShowView(vm);
        }

        private void ShowView(MainViewModel vm)
        {
            // ✅ fixed: correct class name
            var view = new ViewWindow { DataContext = vm };
            MainWindow = view;
            view.Show();
        }

        private static bool HasArg(string[] args, string name) =>
            args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        private async Task ApplyUpdateAndRelaunchAsync()
        {
            string installDir = AppContext.BaseDirectory;
            string extractDir = Path.Combine(Path.GetTempPath(), "WpfAutoUpdater_Extract");

            if (!Directory.Exists(extractDir))
                return;

            // Wait a bit for old process to exit
            await Task.Delay(1000);

            foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(extractDir, file);
                string destPath = Path.Combine(installDir, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, overwrite: true);
            }

            // Relaunch app with --skip-update-check
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(installDir, "WpfAutoUpdater.exe"),
                Arguments = "--skip-update-check",
                UseShellExecute = true
            });
        }
    }
}
