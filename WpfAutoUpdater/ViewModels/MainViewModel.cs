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

        [ObservableProperty]
        private string downloadUrl = string.Empty;

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

                string temp = Path.GetTempPath();
                string tmpZip = Path.Combine(temp, "WpfAutoUpdaterUpdate.zip");
                string tmpDir = Path.Combine(temp, "WpfAutoUpdaterUpdate");

                if (File.Exists(tmpZip)) File.Delete(tmpZip);
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);

                // Use the URL already found by CheckForUpdateAsync if available
                if (string.IsNullOrWhiteSpace(DownloadUrl))
                {
                    var release = await _updater.GetLatestReleaseAsync();
                    DownloadUrl = release.downloadUrl;
                }
                if (string.IsNullOrWhiteSpace(DownloadUrl))
                {
                    throw new InvalidOperationException("No downloadable asset found in the latest release.");
                }

                await _updater.DownloadWithProgressAsync(DownloadUrl!, tmpZip, (bytes, total) =>
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

                // .bat
                string exePath = Process.GetCurrentProcess().MainModule!.FileName!;
                string appDir = Path.GetDirectoryName(exePath)!;
                string updaterBat = Path.Combine(temp, "run_update.bat");

                string lockFile = Path.Combine(temp, $"lock_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(lockFile, "lock");

                // Relaunch arg to ensure the app shows ViewWindow after update
                const string relaunchArg = "--skip-update-check";

                string bat = $@"@echo off
setlocal
set SRC=""{tmpDir}""
set DEST=""{appDir}""
:waitloop
if exist ""{lockFile}"" (
  timeout /t 1 /nobreak >nul
  goto waitloop
)
robocopy ""%SRC%"" ""%DEST%"" /E /R:5 /W:2 /NFL /NDL /NP /NJH /NJS >nul
start """" ""{exePath}"" {relaunchArg}
endlocal
";
// write /E instead of /MIR to avoid deleting files
// write "%USERPROFILE%\Downloads\*" to expect files

                File.WriteAllText(updaterBat, bat, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                // IMPORTANT: Start the batch NOW (while the lock exists) so it waits.
                bool installUnderProgramFiles = appDir.StartsWith(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    StringComparison.OrdinalIgnoreCase)
                    || appDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    StringComparison.OrdinalIgnoreCase);

                if (installUnderProgramFiles)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = updaterBat,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = temp
                    };
                    Process.Start(psi);
                }
                else
                {
                    // We can hide the console by launching via cmd.exe /c with CreateNoWindow
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"\"{updaterBat}\"\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = temp
                    };
                    Process.Start(psi);
                }

                // Delete the lock (so the .bat proceeds)
                AppDomain.CurrentDomain.ProcessExit += (_, __) =>
                {
                    try { File.Delete(lockFile); } catch { }
                };

                Status = "Update ready. The app will restart to complete installation...";
                await Task.Delay(800);

                UpdateCompleted?.Invoke(this, EventArgs.Empty);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Status = $"Update failed: {ex.Message}";
            }
        }
    }
}
