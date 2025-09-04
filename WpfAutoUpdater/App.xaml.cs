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
        // Simple log to verify startup paths:
        private static readonly string LogPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "WpfAutoUpdater", "startup.log");

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Log($"Args: {string.Join(' ', e.Args)}");

            var vm = new MainViewModel();
            var args = e.Args;

            bool skipUpdateCheck = args.Any(a => a.Equals("--skip-update-check", StringComparison.OrdinalIgnoreCase));
            bool hasApply = TryGetArgValue(args, "--apply-update", out var extractDir);
            bool hasTarget = TryGetArgValue(args, "--target", out var targetDir);
            int? parentPid = TryGetArgInt(args, "--pid");

            // 1) Apply-update helper mode (no UI)
            if (hasApply)
            {
                Log($"Mode: APPLY extractDir={extractDir} targetDir={targetDir} pid={parentPid}");
                if (!hasTarget || string.IsNullOrWhiteSpace(targetDir))
                {
                    Log("ERROR: --target is required in apply mode.");
                    Shutdown(-1);
                    return;
                }

                await ApplyUpdateAndRelaunchAsync(extractDir!, targetDir!, parentPid);
                Shutdown();
                return;
            }

            // 2) Skip path → go straight to ViewWindow
            if (skipUpdateCheck)
            {
                Log("Mode: SKIP → show ViewWindow");
                var view = new ViewWindow { DataContext = vm };
                MainWindow = view;
                view.Show();
                return;
            }

            // 3) Normal startup: check before showing anything
            try
            {
                Log("Checking for update...");
                await vm.CheckForUpdateAsync();

                if (vm.IsUpdateAvailable)
                {
                    Log($"Update available: {vm.LatestVersion} → show MainWindow");
                    var main = new MainWindow { DataContext = vm };
                    MainWindow = main;
                    main.Show();
                }
                else
                {
                    Log("No update → show ViewWindow");
                    var view = new ViewWindow { DataContext = vm };
                    MainWindow = view;
                    view.Show();
                }
            }
            catch (Exception ex)
            {
                Log("Check failed → show ViewWindow. " + ex);
                var view = new ViewWindow { DataContext = vm };
                MainWindow = view;
                view.Show();
            }
        }

        private static async Task ApplyUpdateAndRelaunchAsync(string extractDir, string targetDir, int? parentPid)
        {
            try
            {
                if (!Directory.Exists(extractDir))
                {
                    Log("Apply: extractDir missing: " + extractDir);
                    return;
                }

                // Wait for parent PID to exit to release locks
                if (parentPid is int pid && pid > 0)
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        for (int i = 0; i < 250; i++)
                        {
                            if (proc.HasExited) break;
                            await Task.Delay(100);
                        }
                    }
                    catch { /* ignore */ }
                }
                else
                {
                    await Task.Delay(1000);
                }

                Log($"Copy from {extractDir} to {targetDir}");
                CopyDirectory(extractDir, targetDir);

                var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "WpfAutoUpdater.exe");
                var relaunchPath = Path.Combine(targetDir, exeName);

                Log("Relaunch: " + relaunchPath + " --skip-update-check");
                Process.Start(new ProcessStartInfo
                {
                    FileName = relaunchPath,
                    Arguments = "--skip-update-check",
                    UseShellExecute = true,
                    WorkingDirectory = targetDir
                });

                TryDeleteDirectory(extractDir);
            }
            catch (Exception ex)
            {
                Log("Apply failed: " + ex);
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, dirPath);
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceDir, filePath);
                var dest = Path.Combine(targetDir, rel);

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                if (File.Exists(dest))
                {
                    try
                    {
                        var attrs = File.GetAttributes(dest);
                        if (attrs.HasFlag(FileAttributes.ReadOnly))
                            File.SetAttributes(dest, attrs & ~FileAttributes.ReadOnly);
                    }
                    catch { /* ignore */ }
                }

                File.Copy(filePath, dest, overwrite: true);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        // ---------- Arg helpers ----------

        private static bool TryGetArgValue(string[] args, string name, out string? value)
        {
            value = null;
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        value = TrimQuotes(args[i + 1]);
                        return true;
                    }
                }
                if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = TrimQuotes(a[(name.Length + 1)..]);
                    return true;
                }
            }
            return false;

            static string TrimQuotes(string s) => s?.Trim().Trim('"') ?? string.Empty;
        }

        private static int? TryGetArgInt(string[] args, string name)
        {
            return TryGetArgValue(args, name, out var str) && int.TryParse(str, out var val) ? val : (int?)null;
        }

        private static void Log(string text)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            }
            catch { /* ignore */ }
        }
    }
}
