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
        public static Release GetLastReleaseInfo(string owner = "qedsd", string repo = "TheGuideToTheNewEden")
        {
            // 在 Octokit 库中创建一个 GitHubClient 实例。
            var github = new GitHubClient(new ProductHeaderValue("GithubReleaseChecker"));
            // 获取仓库的所有 Release 版本。
            var releases = github.Repository.Release.GetAll(owner, repo).Result;
            // 获取最新版本的 Release。
            return releases.FirstOrDefault();
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
