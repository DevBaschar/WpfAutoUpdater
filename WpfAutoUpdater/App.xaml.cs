// App.xaml.cs
using System.Windows;

namespace WpfAutoUpdater
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool runSilent = e.Args.Any(a => a.Equals("--silent-update", StringComparison.OrdinalIgnoreCase));

            if (runSilent)
            {
                var vm = new ViewModels.MainViewModel();
                // Check quietly
                await vm.CheckForUpdateAsync();
                if (vm.IsUpdateAvailable)
                {
                    // Download + stage, auto-restart via the updater script in ViewModel
                    await vm.DownloadAndInstallAsync();
                    return; // app will shutdown after staging
                }
                Shutdown();
            }
        }
    }
}
