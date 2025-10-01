// Services/GitHubUpdater.cs
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfAutoUpdater.Services;

public class GitHubUpdater
{
    private readonly HttpClient _http = new();

    private const string _owner = "DevBaschar";
    private const string _repo = "WpfAutoUpdater";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public record Asset([property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
                        [property: JsonPropertyName("name")] string Name);
    public record Release([property: JsonPropertyName("tag_name")] string TagName,
                          [property: JsonPropertyName("assets")] List<Asset> Assets);

    public async Task<(string version, string? downloadUrl)> GetLatestReleaseAsync()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest");

        // GitHub API requires a UA header:
        req.Headers.UserAgent.ParseAdd($"{_repo}/1.0 (+https://github.com/DevBaschar/{_repo})");

        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var release = await resp.Content.ReadFromJsonAsync<Release>(_jsonOptions)
                      ?? throw new InvalidOperationException("No release payload.");
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                 ?? release.Assets.FirstOrDefault();

        return (release.TagName, asset?.BrowserDownloadUrl);
    }

    public async Task DownloadWithProgressAsync(string url, string destination, Action<long, long> onProgress, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
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
