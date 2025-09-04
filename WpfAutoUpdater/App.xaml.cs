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

            // 0) If we're in "apply update" helper mode, do the swap then relaunch
            if (HasArg(e.Args, "--apply-update"))
            {
                await ApplyUpdateAndRelaunchAsync(e.Args);
                Shutdown();
                return;
            }

            // Keep your existing flags if you use them
            bool runSilent = HasArg(e.Args, "--silent-update");
            bool skipUpdateCheck = HasArg(e.Args, "--skip-update-check");

            var vm = new MainViewModel();

            if (runSilent)
            {
                try
                {
                    await vm.CheckForUpdateAsync();
                    if (vm.IsUpdateAvailable)
                        await vm.DownloadAndInstallAsync();
                }
                catch { Shutdown(-1); return; }

                Shutdown();
                return;
            }

            // Interactive: if we just applied an update or want to skip, go straight to View
            if (skipUpdateCheck)
            {
                ShowView(vm);
                return;
            }

            // Decide which window to show first
            try
            {
                await vm.CheckForUpdateAsync();
                if (vm.IsUpdateAvailable)
                {
                    // Show MainWindow to let user see progress & start install (your button calls DownloadAndInstallAsync)
                    var updateWindow = new MainWindow { DataContext = vm };
                    MainWindow = updateWindow;
                    updateWindow.Show();
                    return;
                }
            }
            catch
            {
                // ignore and fall through to View
            }

            // No update → go straight to View
            ShowView(vm);
        }

        private static bool HasArg(string[] args, string name) =>
            args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        private static string? GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private async Task ApplyUpdateAndRelaunchAsync(string[] args)
        {
            string? stagingDir = GetArgValue(args, "--apply-update");
            string? fromPidStr = GetArgValue(args, "--from-pid");
            string? exeName = GetArgValue(args, "--exe-name");

            if (string.IsNullOrWhiteSpace(stagingDir) || string.IsNullOrWhiteSpace(exeName))
                return;

            // Wait for the original process to exit so files are unlocked
            if (int.TryParse(fromPidStr, out int pid))
            {
                try { Process.GetProcessById(pid).WaitForExit(15000); } catch { /* already exited */ }
            }

            string installDir = AppContext.BaseDirectory;

            // Copy all files from staging to installDir (now unlocked)
            foreach (var src in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(stagingDir, src);
                string dest = Path.Combine(installDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(src, dest, overwrite: true);
            }

            // Cleanup staging
            try { Directory.Delete(stagingDir, true); } catch { /*ignore*/ }

            // Relaunch the real app from installDir, skipping update check, so we go straight to View.xaml
            var realExe = Path.Combine(installDir, exeName);
            var psi = new ProcessStartInfo
            {
                FileName = realExe,
                Arguments = "--skip-update-check",
                UseShellExecute = true
            };
            Process.Start(psi);

            // If we are a temp copy in %TEMP%, we can optionally delete ourselves after launching (best-effort)
            try
            {
                string self = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(self) && Path.GetDirectoryName(self) == Path.GetTempPath().TrimEnd('\\'))
                {
                    // schedule self-delete via cmd (Windows trick)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C ping 127.0.0.1 -n 2 >NUL & del /Q \"{self}\"",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
            }
            catch { /*best effort*/ }
        }

        private void ShowView(MainViewModel vm)
        {
            var view = new View { DataContext = vm };
            MainWindow = view;
            view.Show();
        }
    }
}
