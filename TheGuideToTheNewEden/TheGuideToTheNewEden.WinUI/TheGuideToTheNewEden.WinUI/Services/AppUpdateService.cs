using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.Helpers;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class AppUpdateService
    {
        public class AppRelease: ObservableObject
        {
            private string _name;
            public string Name { get => _name; set => SetProperty(ref _name, value); }

            private string _url;
            public string Url { get => _url; set => SetProperty(ref _url, value); }

            private string _version;
            public string Version { get => _version; set => SetProperty(ref _version, value); }

            private string _description;
            public string Description { get => _description; set => SetProperty(ref _description, value); }

            private string _date;
            public string Date { get => _date; set => SetProperty(ref _date, value); }
        }
        private List<AppRelease> _releases;
        private AppRelease _lastRelease;
        private bool _isLatest;
        public async Task<string> UpdateReleasesStatusAsync()
        {
            try
            {
                var releases = await GithubHelper.GetReleaseInfoAsync();
                _releases = new List<AppRelease>();
                foreach (var release in releases)
                {
                    if (release.Prerelease)
                        continue;
                    var targetAsset = release.Assets.Where(p => p.Name.EndsWith(".exe")).FirstOrDefault();
                    targetAsset ??= release.Assets.FirstOrDefault();
                    _releases.Add(new AppRelease()
                    {
                        Url = targetAsset?.BrowserDownloadUrl,
                        Version = release.TagName.Replace("v", "", StringComparison.OrdinalIgnoreCase),
                        Description = release.Body,
                        Date = release.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd"),
                        Name = targetAsset?.Name,
                    });
                }
                _lastRelease = _releases.FirstOrDefault();
                if(_lastRelease != null)
                {
                    string curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    _isLatest = new Version(curVersion) >= new Version(_lastRelease.Version);
                }
                return null;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                return ex.Message;//会因为Github的访问次数限制导致失败
            }
        }
        public string GetAppVersion()
        {
            var informationalVersionAttr = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return informationalVersionAttr?.InformationalVersion ?? "Unknown";
        }
        public void GetReleasesStatus(out List<AppRelease> releases, out AppRelease lastRelease, out bool isLatest)
        {
            releases = _releases;
            lastRelease = _lastRelease;
            isLatest = _isLatest;
        }
        public bool Checked()
        {
            return _releases != null;
        }

        private string _installerFilePath = null;
        private object _locker = new object();
        public async Task<string> StartDownload(DownloadDelegate callback, CancellationToken cancellationToken = default)
        {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _installerFilePath = System.IO.Path.Combine(dir, _lastRelease.Name);
            try
            {
                await Task.Run(async () =>
                {
                    bool done = false;
                    var mtd = new MultiThreadDownloader(_lastRelease.Url, Environment.GetEnvironmentVariable("temp"), _installerFilePath, 8);
                    mtd.TotalProgressChanged += (sender, e) =>
                    {
                        var downloader = sender as MultiThreadDownloader;
                        callback.Invoke(downloader.TotalBytesReceived, downloader.Size);
                    };
                    mtd.FileMergedComplete += (sender, e) =>
                    {
                        lock (_locker)
                        {
                            done = true;
                        }
                    };
                    mtd.Start();//开始下载
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            mtd.Pause();
                            break;
                        }
                        lock (_locker)
                        {
                            if (done)
                            {
                                break;
                            }
                        }
                        await Task.Delay(1000);
                    }
                });
                return null;
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return ex.Message;
            }
        }
        public delegate void DownloadDelegate(double downloaded, double total);
        public void StartInstall()
        {
            System.Diagnostics.Process.Start(_installerFilePath);
        }
    }
}
