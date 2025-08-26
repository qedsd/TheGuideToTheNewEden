using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class GithubHelper
    {
        public static string Token {  get; set; }
        public static async Task<Release> GetLastReleaseInfoAsync(string owner = "qedsd", string repo = "TheGuideToTheNewEden")
        {
            var releases = await GetReleaseInfoAsync(owner, repo);
            return releases?.FirstOrDefault();
        }
        public static async Task<IReadOnlyList<Release>> GetReleaseInfoAsync(string owner = "qedsd", string repo = "TheGuideToTheNewEden")
        {
            var github = new GitHubClient(new ProductHeaderValue("GithubReleaseChecker"));
            if (!string.IsNullOrEmpty(Token))
            {
                var tokenAuth = new Credentials(Token);
                github.Credentials = tokenAuth;
            }
            var releases = await github.Repository.Release.GetAll(owner, repo);
            return releases;
        }
        public static async Task<bool> DownloadRelease(Release release, string saveFile)
        {
            using(WebClient webClient = new WebClient())
            {
                try
                {
                    await webClient.DownloadFileTaskAsync(new Uri(release.Url), saveFile);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    return false;
                }
            }
        }

    }
}
