// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Dialogs;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LinksPage : Page
    {
        public LinksPage()
        {
            this.InitializeComponent();
            //this.Background = new ImageBrush()
            //{
            //    ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "home.jpg")))
            //};
            Init();
        }
        private List<Core.Models.LinkInfo> _linkInfos;
        private void Init()
        {
            string filePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"Configs", "Links.json");
            if (!File.Exists(filePath))
            {
                var defaultFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Links.json");
                if (File.Exists(defaultFile))
                {
                    File.Copy(defaultFile, filePath, true);
                }
                else
                {
                    Core.Log.Error("Default Links.json file not found");
                }
            }
            if(File.Exists(filePath))
            {
                string str = File.ReadAllText(filePath);
                if(!string.IsNullOrEmpty(str))
                {
                    _linkInfos = JsonConvert.DeserializeObject<List<Core.Models.LinkInfo>>(str);
                    GridView.ItemsSource = _linkInfos;
                }
                else
                {
                    Core.Log.Error("Links.json is empty");
                }
            }
            else
            {
                Core.Log.Error($"Links.json is not exist: {filePath}");
            }
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var info = e.ClickedItem as Core.Models.LinkInfo;
            if(info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.Url);
            }
        }

        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            var info = image.DataContext as Core.Models.LinkInfo;
            string url;
            if (string.IsNullOrEmpty(info.IconUrl))
            {
                url = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "LinkIcons", "default.png");
            }
            else if (info.IconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = info.IconUrl;
            }
            else
            {
                url = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "LinkIcons", info.IconUrl);
            }
            image.Source = new BitmapImage(new Uri(url));
        }

        private void Save()
        {
            string filePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Configs", "Links.json");
            string json = JsonConvert.SerializeObject(_linkInfos);
            File.WriteAllText(filePath, json);
            (Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow).ShowSuccess("ÒÑ±£´æ");
        }

        private async void MenuFlyoutItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            if(await EditLinkInfoDialog.EditAsync(((sender as FrameworkElement).DataContext as Core.Models.LinkInfo), this.XamlRoot))
            {
                Save();
                GridView.ItemsSource = null;
                GridView.ItemsSource = _linkInfos;
            }
        }

        private async void MenuFlyoutItem_Add_Click(object sender, RoutedEventArgs e)
        {
            var info = await EditLinkInfoDialog.AddAsync(this.XamlRoot);
            if (info != null)
            {
                _linkInfos.Add(info);
                Save();
                GridView.ItemsSource = null;
                GridView.ItemsSource = _linkInfos;
            }
        }

        private void MenuFlyoutItem_Open_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as Core.Models.LinkInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.Url);
            }
        }

        private void MenuFlyoutItem_Copy_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
