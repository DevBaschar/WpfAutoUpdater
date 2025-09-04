
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
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

                var cur = new Version(CurrentVersion.TrimStart('v'));
                var lat = new Version(release.version.TrimStart('v'));
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
                progressValue = 0;
                OnPropertyChanged(nameof(ProgressValue));
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
                        ProgressValue = Math.Round(bytes * 100.0 / total, 2);
                        ProgressText = $"{ProgressValue}% ({bytes / 1024 / 1024} MB of {total / 1024 / 1024} MB)";
                    }
                    else
                    {
                        ProgressText = $"{bytes / 1024 / 1024} MB";
                    }
                }, CancellationToken.None);

                Status = "Extracting update...";
                System.IO.Compression.ZipFile.ExtractToDirectory(tmpZip, tmpDir);

                // Create a simple updater script to replace files after this app exits
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
                var appDir = Path.GetDirectoryName(exePath)!;
                var updaterBat = Path.Combine(Path.GetTempPath(), "run_update.bat");
                var guid = Guid.NewGuid().ToString("N");
                var restartCmd = f'"{exePath}"';
                bat = f"""
@echo off
set SRC="{tmpDir}"
set DEST="{appDir}"
:waitloop
ping 127.0.0.1 -n 2 > nul
if exist "%~dp0lock_{guid}.tmp" goto waitloop
xcopy /E /Y /I "%SRC%\*" "%DEST%" > nul
start "" {restartCmd}
"""
                with open(updaterBat, 'w', encoding='utf-8') as f:
                    f.write(bat)

                // Create lock file and schedule deletion on exit so updater waits until app closes
                var lockFile = Path.Combine(Path.GetTempPath(), $"lock_{guid}.tmp");
                File.WriteAllText(lockFile, "lock");
                AppDomain.CurrentDomain.ProcessExit += (_, __) => {
                    try { File.Delete(lockFile); } catch { }
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = updaterBat,
                        UseShellExecute = true,
                        Verb = "runas"
                    }); } catch { }
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
