using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        [RelayCommand]
        public async Task CheckForUpdateAsync()
        {
            try
            {
                Status = "Checking for updates...";
                var release = await _updater.GetLatestReleaseAsync();
                LatestVersion = release.version;

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

        [RelayCommand]
        public async Task DownloadAndInstallAsync()
        {
            if (!IsUpdateAvailable)
            {
                Status = "No update available.";
                return;
            }

            try
            {
                Status = "Downloading update...";
                ProgressValue = 0;
                ProgressText = string.Empty;

                var tmpZip = Path.Combine(Path.GetTempPath(), "WpfAutoUpdaterUpdate.zip");
                var tmpDir = Path.Combine(Path.GetTempPath(), "WpfAutoUpdaterUpdate");
                if (File.Exists(tmpZip)) File.Delete(tmpZip);
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);

                var release = await _updater.GetLatestReleaseAsync();
                var url = release.downloadUrl;
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("No downloadable asset found in the latest release.");

                await _updater.DownloadWithProgressAsync(url, tmpZip, (bytes, total) =>
                {
                    if (total > 0)
                    {
                        var pct = Math.Round(bytes * 100.0 / total, 2);
                        ProgressValue = pct;
                        ProgressText = $"{pct}% ({bytes / 1024 / 1024} MB of {total / 1024 / 1024} MB)";
                    }
                    else
                    {
                        ProgressText = $"{bytes / 1024 / 1024} MB";
                    }
                }, CancellationToken.None);

                Status = "Extracting update...";
                ZipFile.ExtractToDirectory(tmpZip, tmpDir, overwriteFiles: true);

                // Prepare updater script to copy files after the app exits
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
                var appDir = Path.GetDirectoryName(exePath)!;
                var updaterBat = Path.Combine(Path.GetTempPath(), "run_update.bat");

                // Lock file ensures the updater waits while the app is still closing
                var lockFile = Path.Combine(Path.GetTempPath(), $"lock_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(lockFile, "lock");

                // We'll ask the app to open a specific view after the update.
                const string postUpdateArg = "--post-update=view";

                // Build a .bat script (C# string interpolation + verbatim)
                var bat = $@"@echo off
setlocal
set SRC=""{tmpDir}""
set DEST=""{appDir}""
:waitloop
ping 127.0.0.1 -n 2 > nul
if exist ""{lockFile}"" goto waitloop
xcopy /E /Y /I ""%SRC%\*"" ""%DEST%\"" > nul
start """" ""{exePath}"" {postUpdateArg}
endlocal
";

                File.WriteAllText(updaterBat, bat, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                // On exit: delete the lock (so the .bat proceeds) and run the updater elevated if needed
                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    try { File.Delete(lockFile); } catch { /* ignore */ }
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = updaterBat,
                            UseShellExecute = true,
                            Verb = "runas" // prompt for elevation when copying into Program Files
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch { /* ignore */ }
                };

                Status = "Update ready. The app will restart to complete installation...";
                await Task.Delay(1200);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Status = $"Update failed: {ex.Message}";
            }
        }
    }
}
