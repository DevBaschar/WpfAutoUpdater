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
                await ApplyUpdateAndRelaunchAsync(e.Args);
                Shutdown();
                return;
            }

            // 2) Normal startup (silent mode not requested)
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
                    // Show MainWindow (update UI)
                    var updateWindow = new MainWindow { DataContext = vm };
                    MainWindow = updateWindow;
                    updateWindow.Show();
                    return; // ✅ Only return here if update window is shown
                }
            }
            catch
            {
                // ignore errors → continue to View
            }

            // 3) If no update → show View
            ShowView(vm);
        }


        private void ShowView(MainViewModel vm)
        {
            var view = new View { DataContext = vm };
            MainWindow = view;
            view.Show();
        }

        private static bool HasArg(string[] args, string name) =>
            args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        private async Task ApplyUpdateAndRelaunchAsync(string[] args)
        {
            string installDir = AppContext.BaseDirectory;

            // Where the update was extracted (must match your DownloadAndInstallAsync logic)
            string extractDir = Path.Combine(Path.GetTempPath(), "WpfAutoUpdater_Extract");

            if (!Directory.Exists(extractDir))
                return; // nothing to apply

            // Wait a bit to make sure old process is gone
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
