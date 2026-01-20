using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ESI.NET;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.System;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class HomeViewModel: BaseViewModel
    {
        private bool _gameServerOnline;
        public bool GameServerOnline { get => _gameServerOnline; set => SetProperty(ref _gameServerOnline, value); }

        private int _players;
        public int Players { get => _players; set => SetProperty(ref _players, value); }

        private string _appVersion;
        public string AppVersion { get => _appVersion; set => SetProperty(ref _appVersion, value); }

        private List<int> _marketTypes;
        public List<int> MarketTypes { get => _marketTypes; set => SetProperty(ref _marketTypes, value); }

        private string _gameTime;
        public string GameTime { get => _gameTime; set => SetProperty(ref _gameTime, value); }

        private Microsoft.UI.Dispatching.DispatcherQueueTimer _dispatcherQueueTimer;
        public HomeViewModel()
        {
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _dispatcherQueueTimer = dispatcherQueue.CreateTimer();
            _dispatcherQueueTimer.Interval = TimeSpan.FromSeconds(1);
            _dispatcherQueueTimer.Tick += OnTimerTick;
            _dispatcherQueueTimer.Start();
            AppVersion = ClientServiceHelper.GetRequiredService<AppUpdateService>().GetAppVersion();
            MarketTypes = MarketStarService.Current.GetIds();
            if (MarketTypes == null || MarketTypes.Count == 0)
            {
                MarketTypes = new List<int>() { MarketOrderService.PlexTypeId, MarketOrderService.LargeSkillInjectorTypeId, MarketOrderService.TritaniumTypeId };
            }
        }

        private void OnTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            GameTime = DateTime.UtcNow.ToString("HH:mm:ss");
        }

        public void Init()
        {
            GameServerStatus();
        }
        private void GameServerStatus()
        {
            EsiResponse<ESI.NET.Models.Status.Status> esiResponse = null;
            Task.Run(() =>
            {
                esiResponse = Core.Services.ESIService.GetDefaultEsi().Status.Retrieve().Result;
            }).ContinueWith((task) =>
            {
                Window.DispatcherQueue.SafelyTryEnqueue(() =>
                {
                    if (esiResponse?.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        GameServerOnline = true;
                        Players = esiResponse.Data.Players;
                    }
                    else
                    {
                        GameServerOnline = false;
                        Players = 0;
                    }
                });
                
            });
        }

        public ICommand ToQQCommand => new RelayCommand(() =>
        {
            Helpers.UrlHelper.OpenInBrower("https://jq.qq.com/?_wv=1027&k=m8Ttv1DX");
        });
        public ICommand ToMSCommand => new RelayCommand(() =>
        {
            Helpers.UrlHelper.OpenInBrower("https://apps.microsoft.com/");
        });
        public ICommand ToGithubCommand => new RelayCommand(() =>
        {
            Helpers.UrlHelper.OpenInBrower("https://github.com/qedsd/TheGuideToTheNewEden");
        });

        public ICommand AppUpdateCommand => new RelayCommand(() =>
        {
            ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToUpdate();
        });
        public ICommand AppAboutCommand => new RelayCommand(() =>
        {
            string title = Helpers.ResourcesHelper.GetString("HomePage_About");
            ToolWindow toolWindow = new ToolWindow(title, title, new Views.AboutPage(), WindowTitleStyle.OnlyClose, true, true, true, true, true, 600, 800);
            toolWindow.Activate();
        });
        public ICommand AppLogCommand => new RelayCommand(() =>
        {
            ViewLogWindow viewLogWindow = new ViewLogWindow();
            viewLogWindow.Activate();
        });
        public ICommand AppSettingCommand => new RelayCommand(() =>
        {
            ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToSetting();
        });
        public ICommand ManualCommand => new RelayCommand(() =>
        {
            string fileName = "Manual_en.pdf";
            if (LanguageSelectorService.Value == "zh-CN")
            {
                fileName = "Manual_zh.pdf";
            }
            string file = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Manuals", fileName);
            System.Diagnostics.Process.Start("explorer.exe", file);
        });

        public void Dispose()
        {
            _dispatcherQueueTimer?.Stop();
            _dispatcherQueueTimer = null;
        }
    }
}
