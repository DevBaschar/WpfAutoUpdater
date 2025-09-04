
using Octokit;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAutoUpdater.Services
{
    public class GitHubUpdater
    {
        private readonly GitHubClient _client;
        private const string Owner = "DevBaschar";
        private const string Repo = "WpfAutoUpdater";

        public GitHubUpdater()
        {
            _client = new GitHubClient(new ProductHeaderValue("WpfAutoUpdater"));
        }

        public async Task<(string version, string? downloadUrl)> GetLatestReleaseAsync()
        {
            var release = await _client.Repository.Release.GetLatest(Owner, Repo);
            // Prefer a .zip asset
            var asset = release.Assets.OrderByDescending(a => a.CreatedAt)
                                      .FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        ?? release.Assets.FirstOrDefault();
            return (release.TagName, asset?.BrowserDownloadUrl);
        }

        public async Task DownloadWithProgressAsync(string url, string destination, Action<long, long> onProgress, CancellationToken ct)
        {
            using var http = new HttpClient();
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(destination);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                onProgress(readTotal, total);
            }
        }
    }
}
