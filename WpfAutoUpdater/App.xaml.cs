using System;
using System.Diagnostics;
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

            bool runSilent = e.Args.Any(a =>
                a.Equals("--silent-update", StringComparison.OrdinalIgnoreCase));

            var vm = new MainViewModel();

            if (runSilent)
            {
                try
                {
                    await vm.CheckForUpdateAsync();
                    if (vm.IsUpdateAvailable)
                        await vm.DownloadAndInstallAsync();
                }
                catch
                {
                    Shutdown(-1);
                }

                Shutdown();
                return;
            }

            // 🚀 Normal mode
            try
            {
                await vm.CheckForUpdateAsync();

                if (vm.IsUpdateAvailable)
                {
                    // 👉 Show MainWindow for interactive update
                    var updateWindow = new MainWindow
                    {
                        DataContext = vm
                    };

                    // When update finishes, switch to View.xaml
                    vm.UpdateCompleted += (_, __) =>
                    {
                        updateWindow.Close();

                        var viewWindow = new View
                        {
                            DataContext = vm
                        };
                        MainWindow = viewWindow;
                        viewWindow.Show();
                    };

                    MainWindow = updateWindow;
                    updateWindow.Show();
                    return;
                }
            }
            catch
            {
                // log if needed, fallback to UI
            }

            // 👉 No update → go directly to View.xaml
            var view = new View
            {
                DataContext = vm
            };
            MainWindow = view;
            view.Show();
        }
    }
}
