using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HDRScreenshotTool;

public class RepoStats
{
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int OpenIssues { get; set; }
    public int Watchers { get; set; }
    public int TotalDownloads { get; set; }
    public string? Error { get; set; }
}

public static class GitHubStatsService
{
    private const string Owner = "hzhxiongmao";
    private const string Repo = "HDR-Screenshot-Tool";
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "HDRScreenshotTool" } },
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<RepoStats> FetchAsync()
    {
        try
        {
            // Fetch repo info
            var repo = await _http.GetFromJsonAsync<GitHubRepo>(
                $"https://api.github.com/repos/{Owner}/{Repo}");

            // Fetch releases for download count
            var releases = await _http.GetFromJsonAsync<List<GitHubRelease>>(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=100");

            int downloads = 0;
            if (releases != null)
            {
                foreach (var rel in releases)
                {
                    if (rel.Assets != null)
                    {
                        foreach (var asset in rel.Assets)
                            downloads += asset.DownloadCount;
                    }
                }
            }

            return new RepoStats
            {
                Stars = repo?.Stars ?? 0,
                Forks = repo?.Forks ?? 0,
                OpenIssues = repo?.OpenIssues ?? 0,
                Watchers = repo?.Subscribers ?? 0,
                TotalDownloads = downloads
            };
        }
        catch (Exception ex)
        {
            return new RepoStats { Error = ex.Message };
        }
    }

    private class GitHubRepo
    {
        [JsonPropertyName("stargazers_count")]
        public int Stars { get; set; }
        [JsonPropertyName("forks_count")]
        public int Forks { get; set; }
        [JsonPropertyName("open_issues_count")]
        public int OpenIssues { get; set; }
        [JsonPropertyName("subscribers_count")]
        public int Subscribers { get; set; }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("download_count")]
        public int DownloadCount { get; set; }
    }
}
