using System.Net.Http.Headers;
using System.Text.Json;

namespace Throne.Api.Cli;

public sealed record ReleaseAsset(string Name, string DownloadUrl);

public sealed record ReleaseInfo(string Tag, IReadOnlyList<ReleaseAsset> Assets)
{
    /// <summary>RID asset names the release workflow publishes (tar.gz on unix, zip on win).</summary>
    public ReleaseAsset? FindAsset(string runtimeIdentifier)
    {
        var tarName = $"throne-{runtimeIdentifier}.tar.gz";
        var zipName = $"throne-{runtimeIdentifier}.zip";
        return Assets.FirstOrDefault(a =>
            string.Equals(a.Name, tarName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, zipName, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Reads the <c>latest</c> GitHub release for the throne repo. The repo slug
/// defaults to the upstream and is overridable via <c>THRONE_UPDATE_REPO</c> so a
/// fork can point self-update at its own releases.
/// </summary>
public static class GitHubReleaseClient
{
    private const string DefaultRepository = "gently-whitesnow/throne";

    public static string Repository =>
        Environment.GetEnvironmentVariable("THRONE_UPDATE_REPO") is { Length: > 0 } r
            ? r
            : DefaultRepository;

    public static async Task<ReleaseInfo> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);

        var url = $"https://api.github.com/repos/{Repository}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("throne-update", ThroneVersion.Current));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                var dl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(dl))
                {
                    assets.Add(new ReleaseAsset(name, dl));
                }
            }
        }

        return new ReleaseInfo(tag, assets);
    }
}
