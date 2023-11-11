using Newtonsoft.Json.Linq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Class
{
    class RetrieveGithubFiles
    {

        public static string RepoOwner = "Babyhamsta";
        public static string RepoName = "Aimmy";
        public static string RepoPath = "models";

        // This is a proof of concept atm so I yanked the code from this:
        // https://stackoverflow.com/questions/46302570/how-to-get-list-of-files-from-a-specific-github-repo-given-a-link-in-c
        // nori

        public static async Task<IEnumerable<string>> ListContents()
        {
            // no github api key added for obv. reasons, might add option to self add one later
            var client = new GitHubClient(new ProductHeaderValue("Github-API-Test"));
            // client.Credentials = ... // Set credentials here, otherwise harsh rate limits apply.

            var contents = await client.Repository.Content.GetAllContents(RepoOwner, RepoName, RepoPath);
            return contents.Select(content => content.Name);
        }
    }
}
