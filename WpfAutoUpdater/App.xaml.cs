using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WpfAutoUpdater
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Avoid async void in OnStartup; delegate to an async Task.
            _ = BootstrapAsync(e);
        }

        private async Task BootstrapAsync(StartupEventArgs e)
        {
            bool runSilent = e.Args.Any(a => a.Equals("--silent-update", StringComparison.OrdinalIgnoreCase));

            var vm = new ViewModels.MainViewModel();

            if (runSilent)
            {
                try
                {
                    // Silent path: check + install, then exit (VM expected to restart/exit).
                    await vm.CheckForUpdateAsync();
                    if (vm.IsUpdateAvailable)
                    {
                        await vm.DownloadAndInstallAsync();
                        // If DownloadAndInstallAsync restarts the app or shuts down, just return.
                        return;
                    }

                    // No update, just close silently.
                    Shutdown();
                    return;
                }
                catch (Exception)
                {
                    // In silent mode, fail closed.
                    Shutdown(-1);
                    return;
                }
            }

            // Interactive path:
            try
            {

                await vm.CheckForUpdateAsync();
                if (vm.IsUpdateAvailable)
                {
                    await vm.DownloadAndInstallAsync(); // returns Task, no assignment
                    // If the app restarted/shut down during install, code below won't run.
                }
            }
            catch (Exception ex)
            {
                // Log exception if you have logging, but don't block startup.
                // e.g., Logger.Error(ex, "Update check/apply failed");
            }

            // 3) If we are still here, either no update was needed, or update failed but we proceed.
            var mainWindow = new View
            {
                DataContext = vm
            };

            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private static void RestartApp(string[] originalArgs)
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    var args = string.Join(" ", originalArgs ?? Array.Empty<string>());
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = args,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Swallow and just continue to shutdown.
            }
            Current.Shutdown();
        }
    }
}
