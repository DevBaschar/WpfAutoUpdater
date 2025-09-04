using System;
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

            var vm = new MainViewModel();
            bool skip = e.Args.Any(a => a.Equals("--skip-update-check", StringComparison.OrdinalIgnoreCase));

            // 1) After update relaunch → go straight to ViewWindow.
            if (skip)
            {
                ShowView(vm);
                return;
            }

            // 2) Normal startup: check BEFORE showing any window.
            try
            {
                await vm.CheckForUpdateAsync();

                if (vm.IsUpdateAvailable)
                {
                    // Update exists → show the updater window so the user can install
                    ShowUpdater(vm);
                }
                else
                {
                    // No update → skip MainWindow and go directly to ViewWindow
                    ShowView(vm);
                }
            }
            catch
            {
                // If the check fails (e.g., offline), fail open to ViewWindow
                ShowView(vm);
            }
        }

        private void ShowView(MainViewModel vm)
        {
            var view = new ViewWindow(vm.CurrentVersion) { DataContext = vm };
            MainWindow = view;
            view.Show();
        }

        private void ShowUpdater(MainViewModel vm)
        {
            var main = new MainWindow { DataContext = vm };
            MainWindow = main;
            main.Show();
        }
    }
}
