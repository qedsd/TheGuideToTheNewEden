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
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ShellPage : Page, IPage
    {
        public ShellPage()
        {
            this.InitializeComponent();
            BannerImage.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "home.jpg")));
        }
        private void ImageBrush_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as ToolItem;
            if (item != null && item.PageType != null)
            {
                VM.Dispose();
                if(ContentFrame.Navigate(item.PageType))
                {
                    NavigationService.SetNavigateTo(item.Title);
                }
                else
                {
                    this.ShowError("导航页面失败");
                }
            }
        }

        public void Close()
        {
            (ContentFrame.Content as IPage)?.Close();
        }
    }
}
