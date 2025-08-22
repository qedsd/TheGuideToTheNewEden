using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Views.Character;
using TheGuideToTheNewEden.WinUI.Views.Map;
using Windows.Foundation;
using Windows.Foundation.Collections;
using NavigationViewItem = TheGuideToTheNewEden.WinUI.Models.NavigationViewItem;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class MainPage : UserControl
    {
        private string _version;
        private List<NavigationViewItem> _navigationViewItems;
        public MainPage()
        {
            InitMenu();
            Loaded += MainPage_Loaded;
            this.InitializeComponent();
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().Init(NavPanel,ContentFrame, Loading, InfoBar);
            MenuList.ItemsSource = _navigationViewItems;
            _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            VersionTextBlock.Text = _version.ToString();
            TryMoveUpdater();
            if (AutoUpdateService.Value)
            {
                CheckUpdate();
            }
        }

        private void InitMenu()
        {
            _navigationViewItems = new List<NavigationViewItem>();

            _navigationViewItems.Add(new Models.NavigationViewItem(typeof(HomePage2)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(CharactersShellPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MarketPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(BusinessPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(GamePreviewMgrPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelIntelPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelMonitorPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelScanPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelMarketPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ChannelTranslationPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(GameLogMonitorPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(TranslationPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(DEDPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MissionPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(WormholePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(LinksPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(MapPage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(ZKBHomePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(DatabasePage)));
            _navigationViewItems.Add(new NavigationViewItem(typeof(SettingPage)));
        }

        public void Dispose()
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>()?.Dispose();
        }

        private void MenuList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = MenuList.SelectedItem as NavigationViewItem;
            if (item != null)
            {
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().NavigateTo(item.Type);
            }
        }

        #region 更新
        private async void CheckUpdate()
        {
            try
            {
                //会因为Github的访问次数限制导致失败
                var release = await System.Threading.Tasks.Task.Run(() => Core.Helpers.GithubHelper.GetLastReleaseInfo());
                if (release != null)
                {
                    var tagName = release.TagName;
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        var lastVersion = tagName.Replace("v", "", StringComparison.OrdinalIgnoreCase);
                        var curVersion = _version.Replace("v", "", StringComparison.OrdinalIgnoreCase);
                        if (lastVersion != curVersion)
                        {
                            ContentDialog contentDialog = new ContentDialog();
                            contentDialog.Title = "有可用更新";
                            contentDialog.Content = new TextBlock()
                            {
                                Text = release.Body,
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                            };
                            contentDialog.XamlRoot = WindowHelper.GetWindowForElement(this).Content.XamlRoot;
                            contentDialog.PrimaryButtonText = "更新";
                            contentDialog.SecondaryButtonText = "取消";
                            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                List<string> args = new List<string>()
                                {
                                    release.TagName,
                                    release.Body,
                                    release.Assets.FirstOrDefault()?.BrowserDownloadUrl
                                };
                                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater", "TheGuideToTheNewEden.Updater.exe"), args);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex.Message);
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(Name, ex.Message, Controls.InfoBarControl.InfoType.Error, false, "检查更新失败");
            }
        }
        private static void TryMoveUpdater()
        {
            try
            {
                //更新器默认放置于同目录下，需要移动到Updater文件夹下
                var updaterFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Where(p => p.Contains(".Updater")).ToList();
                if (updaterFiles.Any())
                {
                    string updaterFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater");
                    if (!Directory.Exists(updaterFolder))
                    {
                        Directory.CreateDirectory(updaterFolder);
                    }
                    foreach (var file in updaterFiles)
                    {
                        System.IO.File.Copy(file, System.IO.Path.Combine(updaterFolder, System.IO.Path.GetFileName(file)), true);
                        System.IO.File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex.Message);
            }
        }
        #endregion
    }
}
