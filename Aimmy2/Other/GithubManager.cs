using Newtonsoft.Json;
using System.Net.Http;
using System.Text.Json;

namespace Aimmy2.Other
{
    internal class GithubManager
    {
        private readonly HttpClient httpClient;

        public GithubManager()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Aimmy2");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        private class GitHubContent
        {
            public string name { get; set; }
        }

        public async Task<(string tagName, string downloadUrl)> GetLatestReleaseInfo(string owner, string repo)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using var response = await httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            string tagName = data["tag_name"].ToString() ?? throw new InvalidOperationException("Tag name is missing in the response");
            string downloadUrl = ((JsonElement)data["assets"]).EnumerateArray().First().GetProperty("browser_download_url").ToString();

            return (tagName, downloadUrl);
        }

        public async Task<IEnumerable<string?>> FetchGithubFilesAsync(string url)
        {
            var response = await httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            List<GitHubContent>? contents = JsonConvert.DeserializeObject<List<GitHubContent>>(content);
            if (contents == null)
            {
                throw new InvalidOperationException("Failed to deserialize GitHub content or Github content is empty.");
            }

            return contents.Select(c => c.name);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}