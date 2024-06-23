using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
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
        }

        public class GitHubFile
        { // the lowercase is intentional - learned the hard way.
            public string? name { get; set; }
            public string? download_url { get; set; }
            public string? sha { get; set; }
        }

        public async Task<(string tagName, string downloadUrl)> GetLatestReleaseInfo(string owner, string repo)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using var response = await httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            string tagName = data["tag_name"].ToString() ?? throw new InvalidOperationException("Tag name is missing in the response");
            string downloadUrl = ((JsonElement)data["assets"]).EnumerateArray().First().GetProperty("browser_download_url").ToString();

            return (tagName, downloadUrl);
        }

        public static string ConvertToApiUrl(string url)
        {
            // Extract the owner, repo, and branch/path dynamically
            var githubBaseUrl = "https://github.com/";
            var apiBaseUrl = "https://api.github.com/repos/";
            var treeIndicator = "/tree/";

            if (!url.StartsWith(githubBaseUrl)) throw new ArgumentException("Invalid GitHub URL");

            int baseLength = githubBaseUrl.Length;
            int treeIndex = url.IndexOf(treeIndicator, baseLength);

            if (treeIndex == -1) throw new ArgumentException("URL must include branch/path");

            string repoPart = url[baseLength..treeIndex]; // Owner/Repo
            string branchAndPath = url[(treeIndex + treeIndicator.Length)..];
            int slashIndex = branchAndPath.IndexOf('/');

            string branch = slashIndex != -1 ? branchAndPath[..slashIndex] : branchAndPath;
            string path = slashIndex != -1 ? branchAndPath[(slashIndex + 1)..] : "";

            return $"{apiBaseUrl}{repoPart}/contents/{path}?ref={branch}";
        }

        public static string ConvertToShortURL(string url)
        {
            const string githubBaseUrl = "https://github.com/";
            const string treeIndicator = "/tree/";

            if (!url.StartsWith(githubBaseUrl)) throw new ArgumentException("Invalid GitHub URL");

            int baseLength = githubBaseUrl.Length;
            int treeIndex = url.IndexOf(treeIndicator, baseLength);

            if (treeIndex == -1) throw new ArgumentException("URL must include branch/path");

            string repoPart = url[baseLength..treeIndex]; // Owner/Repo
            string path = url[(treeIndex + treeIndicator.Length)..]; // Branch and Path

            return $"{repoPart}{path}";
        }

        public async Task<Dictionary<string, GitHubFile>> FetchGithubFilesAsync(string apiUrl)
        {
            if (httpClient == null)
            {
                throw new InvalidOperationException("httpClient is null");
            }

            using var responseStream = await httpClient.GetStreamAsync(apiUrl);

            if (responseStream == null)
            {
                throw new InvalidOperationException("responseStream is null");
            }

            var items = await JsonSerializer.DeserializeAsync<List<GitHubFile>>(responseStream);
            var allFiles = new Dictionary<string, GitHubFile>();

            if (items == null)
            {
                return allFiles;
            }
            HashSet<string> fileExtensions = new(StringComparer.OrdinalIgnoreCase) { ".onnx", ".cfg" };

            foreach (var item in items)
            {
                if (item?.name == null)
                {
                    continue;
                }

                if (!fileExtensions.Contains(Path.GetExtension(item.name)))
                {
                    continue;
                }

                allFiles[item.name] = item;

                //    allFiles.Add(item.name!, new GitHubFile
                //    {
                //        name = item.name,
                //        download_url = item.download_url,
                //        sha = item.sha
                //    });
            }

            return allFiles;
        }

        public static Dictionary<string, string> GetLocalFileShas(string localDir)
        {
            var files = new Dictionary<string, string>();

            foreach (var filePath in Directory.EnumerateFiles(localDir))
            {
                using var stream = File.OpenRead(filePath);
                var sha = BitConverter.ToString(SHA1.Create().ComputeHash(stream)).Replace("-", "").ToLower();
                var fileName = Path.GetFileName(filePath);

                files[fileName] = sha;
            }

            return files;
        }

        //public async Task DownloadFileAsync(string url, string localPath)
        //{
        //    using var response = await httpClient.GetAsync(url);
        //    response.EnsureSuccessStatusCode();

        //    using var contentStream = await response.Content.ReadAsStreamAsync();
        //    using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        //    await contentStream.CopyToAsync(fileStream);
        //}

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}