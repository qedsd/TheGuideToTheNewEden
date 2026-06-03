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
using Microsoft.UI.Xaml.Controls;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
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

        private string _checkUpdateError;
        public string CheckUpdateError { get => _checkUpdateError; set => SetProperty(ref _checkUpdateError, value); }

        private bool _hasCheckUpdateError;
        public bool HasCheckUpdateError { get => _hasCheckUpdateError; set => SetProperty(ref _hasCheckUpdateError, value); }

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
            CheckUpdate();
        }
        private void GameServerStatus()
        {
            EsiResponse<ESI.NET.Models.Status.Status> esiResponse = null;
            Task.Run(() =>
            {
                try
                {
                    esiResponse = Core.Services.ESIService.GetDefaultEsi().Status.Retrieve().Result;
                }
                catch{ }
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
        private async void CheckUpdate()
        {
            if (AutoUpdateService.Value)
            {
                try
                {
                    var failedMsg = await ClientServiceHelper.GetRequiredService<AppUpdateService>().UpdateReleasesStatusAsync();
                    if (!string.IsNullOrEmpty(failedMsg))
                    {
                        HasCheckUpdateError = true;
                        CheckUpdateError = failedMsg;
                    }
                    else
                    {
                        HasCheckUpdateError = false;
                        CheckUpdateError = null;
                        ClientServiceHelper.GetRequiredService<AppUpdateService>().GetReleasesStatus(out var releases, out var lastRelease, out var isLatest);
                        if (!isLatest)
                        {
                            ContentDialog contentDialog = new ContentDialog();
                            contentDialog.Title = $"{Helpers.ResourcesHelper.GetString("Update_FoundLastVersion")} {lastRelease.Version}";
                            contentDialog.Content = new TextBlock()
                            {
                                Text = lastRelease.Description,
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                            };
                            contentDialog.XamlRoot = Helpers.WindowHelper.MainWindow.Content.XamlRoot;
                            contentDialog.PrimaryButtonText = Helpers.ResourcesHelper.GetString("Update_ConfirmUpdate");
                            contentDialog.SecondaryButtonText = Helpers.ResourcesHelper.GetString("Update_NotUpdate");
                            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToUpdate();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex.Message);
                    HasCheckUpdateError = true;
                    CheckUpdateError = ex.Message;
                }
            }
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
            HasCheckUpdateError = false;
            CheckUpdateError = null;
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
