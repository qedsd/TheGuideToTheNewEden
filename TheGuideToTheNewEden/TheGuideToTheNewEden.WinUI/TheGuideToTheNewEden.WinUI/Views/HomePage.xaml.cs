using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class HomePage : Page
    {
        private BaseWindow _window;
        public HomePage()
        {
            this.InitializeComponent();
            Loaded += HomePage_Loaded;
        }
        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            TabView_AddTabButtonClick(TabView, null);
            CheckUpdate();
            _window.SetTitleBar(CustomDragRegion);
            Loaded -= HomePage_Loaded;
        }
        #region ����
        private static string GetVersionDescription()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        private async void CheckUpdate()
        {
            TryMoveUpdater();
            try
            {
                //����ΪGithub�ķ��ʴ������Ƶ���ʧ��
                var release = await System.Threading.Tasks.Task.Run(() => Core.Helpers.GithubHelper.GetLastReleaseInfo());
                if (release != null)
                {
                    var tagName = release.TagName;
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        var lastVersion = tagName.Replace("v", "", StringComparison.OrdinalIgnoreCase);
                        var curVersion = GetVersionDescription().Replace("v", "", StringComparison.OrdinalIgnoreCase);
                        if (lastVersion != curVersion)
                        {
                            ContentDialog contentDialog = new ContentDialog();
                            contentDialog.Title = "�п��ø���";
                            contentDialog.Content = new TextBlock()
                            {
                                Text = release.Body,
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                            };
                            contentDialog.XamlRoot = WindowHelper.GetWindowForElement(this).Content.XamlRoot;
                            contentDialog.PrimaryButtonText = "����";
                            contentDialog.SecondaryButtonText = "ȡ��";
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
                (WindowHelper.GetWindowForElement(this) as BaseWindow).ShowError("��ȡ����ʧ��", true);
            }
        }
        private static void TryMoveUpdater()
        {
            try
            {
                //������Ĭ�Ϸ�����ͬĿ¼�£���Ҫ�ƶ���Updater�ļ�����
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

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabViewItem item = new TabViewItem()
            {
                Header = Helpers.ResourcesHelper.GetString("HomePage_Home"),
                IsSelected = true,
            };
            ShellPage content = new ShellPage(this);
            item.Content = content;
            sender.TabItems.Add(item);
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            ((args.Item as TabViewItem).Content as IPage)?.Close();
            sender.TabItems.Remove(args.Item);
            if(!sender.TabItems.Any())
            {
                TabView_AddTabButtonClick(sender, null);
            }
        }

        public void SetNavigateTo(string title)
        {
            (TabView.SelectedItem as TabViewItem).Header = title;
        }
    }
}