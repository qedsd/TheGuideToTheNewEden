using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ShellPage : Page
    {
        public ShellPage()
        {
            this.InitializeComponent();
            Loaded += ShellPage_Loaded;
            BannerImage.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "home.jpg")));
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            CheckUpdate();
        }
        private async void CheckUpdate()
        {
            //List<string> args2 = new List<string>()
            //                {
            //                    "V1",
            //                    "Test",
            //                    "https://github.com/qedsd/TheGuideToTheNewEden/releases/download/v2.1.0.0/TheGuideToTheNewEden_v2.1.0.zip"
            //                };
            //Process pro = Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Updater","TheGuideToTheNewEden.Updater.exe"), args2);

            //return;
            TryMoveUpdater();
            try
            {
                //会因为Github的访问次数限制导致失败
                var release = await System.Threading.Tasks.Task.Run(()=>Core.Helpers.GithubHelper.GetLastReleaseInfo());
                if (release != null)
                {
                    var tagName = release.TagName;
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        var lastVersion = tagName.Replace("v", "", StringComparison.OrdinalIgnoreCase);
                        var curVersion = VM.VersionDescription.Replace("v", "", StringComparison.OrdinalIgnoreCase);
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
                                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater","TheGuideToTheNewEden.Updater.exe"), args);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex.Message);
                (WindowHelper.GetWindowForElement(this) as BaseWindow).ShowError("获取更新失败", true);
            }
        }
        private void TryMoveUpdater()
        {
            try
            {
                //更新器默认放置于同目录下，需要移动到Updater文件夹下
                var updaterFiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory).Where(p=>p.Contains(".Updater")).ToList();
                if(updaterFiles.Any())
                {
                    string updaterFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater");
                    if(!Directory.Exists(updaterFolder))
                    {
                        Directory.CreateDirectory(updaterFolder);
                    }
                    foreach(var file in  updaterFiles)
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
        private void ImageBrush_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ToolItem;
            if (item != null && item.PageType != null)
            {
                var obj = System.Activator.CreateInstance(item.PageType);
                var win = new BaseWindow(item.Title)
                {
                    MainContent = obj
                };
                Helpers.WindowHelper.TrackWindow(win);
                win.Activate();
            }
        }
    }
}
