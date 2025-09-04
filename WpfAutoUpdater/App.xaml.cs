using System;
using System.Linq;
using System.Windows;

namespace WpfAutoUpdater
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Detect silent mode: run as a background updater and exit.
            bool runSilent = e.Args.Any(a =>
                a.Equals("--silent-update", StringComparison.OrdinalIgnoreCase));

            var vm = new ViewModels.MainViewModel();

            if (runSilent)
            {
                try
                {
                    // Silent: check + install if available, then exit.
                    await vm.CheckForUpdateAsync();

                    if (vm.IsUpdateAvailable)
                    {
                        await vm.DownloadAndInstallAsync();
                        // If your updater restarts or shuts down, we won't reach further code.
                        Shutdown(); // Exit silently if we returned here (no-op otherwise).
                        return;
                    }

                    Shutdown(); // No update; exit silently.
                    return;
                }
                catch
                {
                    // Fail closed in silent mode
                    Shutdown(-1);
                    return;
                }
            }

            // Interactive path
            try
            {
                // Check first before showing the main window
                await vm.CheckForUpdateAsync();

                if (vm.IsUpdateAvailable)
                {
                    // Download + install. If your method restarts/shuts down,
                    // code below will not execute in the old instance.
                    await vm.DownloadAndInstallAsync();
                }
            }
            catch
            {
                // Optionally log the failure and continue to launch the UI.
                // e.g., Logger.Error(ex, "Update check/apply failed");
            }

            // If we're still here: either no update, update failed, or update completed without restart.
            var mainWindow = new View
            {
                DataContext = vm
            };

            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
