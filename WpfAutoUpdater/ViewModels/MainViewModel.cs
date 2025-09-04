using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfAutoUpdater.Helpers;
using WpfAutoUpdater.Services;

namespace WpfAutoUpdater.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GitHubUpdater _updater = new();

        [ObservableProperty]
        private string status = "Ready";

        [ObservableProperty]
        private string currentVersion = VersionHelper.GetCurrentVersion();

        [ObservableProperty]
        private string latestVersion = "-";

        [ObservableProperty]
        private bool isUpdateAvailable;

        [ObservableProperty]
        private double progressValue;

        [ObservableProperty]
        private string progressText = string.Empty;

        public event EventHandler? UpdateCompleted;

        [RelayCommand]
        public async Task CheckForUpdateAsync()
        {
            try
            {
                Status = "Checking for updates...";
                var release = await _updater.GetLatestReleaseAsync();
                LatestVersion = release.version;
                DownloadUrl = release.downloadUrl;

                // Make version parsing a bit more resilient
                var curStr = (CurrentVersion ?? "0.0").TrimStart('v', 'V');
                var latStr = (release.version ?? "0.0").TrimStart('v', 'V');

                if (!Version.TryParse(curStr, out var cur))
                    cur = new Version(0, 0);

                if (!Version.TryParse(latStr, out var lat))
                    lat = cur;

                IsUpdateAvailable = lat > cur;
                Status = IsUpdateAvailable ? $"Update available: {release.version}" : "You are up to date.";
            }
            catch (Exception ex)
            {
                Status = $"Error checking updates: {ex.Message}";
            }
        }

        //        [RelayCommand]
        //        public async Task DownloadAndInstallAsync()
        //        {
        //            if (!IsUpdateAvailable)
        //            {
        //                Status = "No update available.";
        //                return;
        //            }

        //            try
        //            {
        //                Status = "Downloading update...";
        //                ProgressValue = 0;
        //                ProgressText = string.Empty;

        //                var tmpZip = Path.Combine(Path.GetTempPath(), "WpfAutoUpdaterUpdate.zip");
        //                var tmpDir = Path.Combine(Path.GetTempPath(), "WpfAutoUpdaterUpdate");
        //                if (File.Exists(tmpZip)) File.Delete(tmpZip);
        //                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        //                Directory.CreateDirectory(tmpDir);

        //                var release = await _updater.GetLatestReleaseAsync();
        //                var url = release.downloadUrl;
        //                if (string.IsNullOrWhiteSpace(url))
        //                    throw new InvalidOperationException("No downloadable asset found in the latest release.");

        //                await _updater.DownloadWithProgressAsync(url, tmpZip, (bytes, total) =>
        //                {
        //                    if (total > 0)
        //                    {
        //                        var pct = Math.Round(bytes * 100.0 / total, 2);
        //                        ProgressValue = pct;
        //                        ProgressText = $"{pct}% ({bytes / 1024 / 1024} MB of {total / 1024 / 1024} MB)";
        //                    }
        //                    else
        //                    {
        //                        ProgressText = $"{bytes / 1024 / 1024} MB";
        //                    }
        //                }, CancellationToken.None);

        //                Status = "Extracting update...";
        //                ZipFile.ExtractToDirectory(tmpZip, tmpDir, overwriteFiles: true);

        //                // Prepare updater script to copy files after the app exits
        //                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        //                var appDir = Path.GetDirectoryName(exePath)!;
        //                var updaterBat = Path.Combine(Path.GetTempPath(), "run_update.bat");

        //                // Lock file ensures the updater waits while the app is still closing
        //                var lockFile = Path.Combine(Path.GetTempPath(), $"lock_{Guid.NewGuid():N}.tmp");
        //                File.WriteAllText(lockFile, "lock");

        //                // We'll ask the app to open a specific view after the update.
        //                const string postUpdateArg = "--post-update=view";

        //                // Build a .bat script (C# string interpolation + verbatim)
        //                var bat = $@"@echo off
        //setlocal
        //set SRC=""{tmpDir}""
        //set DEST=""{appDir}""
        //:waitloop
        //ping 127.0.0.1 -n 2 > nul
        //if exist ""{lockFile}"" goto waitloop
        //xcopy /E /Y /I ""%SRC%\*"" ""%DEST%\"" > nul
        //start """" ""{exePath}"" {postUpdateArg}
        //endlocal
        //";

        //                File.WriteAllText(updaterBat, bat, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        //                // On exit: delete the lock (so the .bat proceeds) and run the updater elevated if needed
        //                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
        //                {
        //                    try { File.Delete(lockFile); } catch { /* ignore */ }
        //                    try
        //                    {
        //                        var psi = new System.Diagnostics.ProcessStartInfo
        //                        {
        //                            FileName = updaterBat,
        //                            Verb = "runas", // prompt for elevation when copying into Program 
        //                            //UseShellExecute = true,               
        //                            UseShellExecute = false,               // required for CreateNoWindow to work
        //                            CreateNoWindow = true,                 // hide 
        //                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        //                            RedirectStandardOutput = true,         // optional: capture output
        //                            RedirectStandardError = true
        //                        };
        //                        System.Diagnostics.Process.Start(psi);
        //                    }
        //                    catch { /* ignore */ }
        //                };

        //                Status = "Update ready. The app will restart to complete installation...";
        //                await Task.Delay(1200);
        //                System.Windows.Application.Current.Shutdown();
        //            }
        //            catch (Exception ex)
        //            {
        //                Status = $"Update failed: {ex.Message}";
        //            }
        //        }



        public string? DownloadUrl { get; private set; }


        [RelayCommand]
        public async Task DownloadAndInstallAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(DownloadUrl))
                throw new InvalidOperationException("No download URL available.");

            string tempZip = Path.Combine(Path.GetTempPath(), "WpfAutoUpdater.zip");
            string extractDir = Path.Combine(Path.GetTempPath(), "WpfAutoUpdater_Extract");
            string installDir = AppContext.BaseDirectory;
            string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine process path.");
            string exeName = Path.GetFileName(exePath);

            // 1) Download
            Status = "Downloading update...";
            ProgressValue = 0;
            ProgressText = string.Empty;

            await _updater.DownloadWithProgressAsync(
                DownloadUrl,
                tempZip,
                (received, total) =>
                {
                    ProgressValue = total > 0 ? (received * 100.0 / total) : 0;
                    ProgressText = $"{received / 1024 / 1024} MB / {total / 1024 / 1024} MB";
                },
                ct);

            // 2) Extract to staging
            Status = "Extracting update...";
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            ZipFile.ExtractToDirectory(tempZip, extractDir);

            // 3) Spawn helper from %TEMP% (copy of this EXE), then quit
            Status = "Preparing to apply update...";
            ProgressText = "Starting apply helper";

            // Copy the current EXE to temp so the original EXE isn't locked during copy
            string helperPath = Path.Combine(Path.GetTempPath(), exeName); // same name in temp is fine
            File.Copy(exePath, helperPath, overwrite: true);

            // Start helper with arguments:
            // --apply-update "<stagingDir>" --from-pid <currentpid> --exe-name "<exeName>"
            var psi = new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = $"--apply-update \"{extractDir}\" --from-pid {Environment.ProcessId} --exe-name \"{exeName}\"",
                UseShellExecute = true
            };
            Process.Start(psi);

            // Close current app so files can be replaced
            Application.Current.Shutdown();
        }
    }
}
