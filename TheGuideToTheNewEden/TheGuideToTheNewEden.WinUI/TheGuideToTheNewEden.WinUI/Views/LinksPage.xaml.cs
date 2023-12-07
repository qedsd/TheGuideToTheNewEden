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
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LinksPage : Page
    {
        public LinksPage()
        {
            this.InitializeComponent();
            //ImageBrush_Backgroud.ImageSource = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "home.jpg")));
            Init();
        }
        private void Init()
        {
            var filePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources","Configs", "Links.json");
            if(File.Exists(filePath))
            {
                string str = File.ReadAllText(filePath);
                if(!string.IsNullOrEmpty(str))
                {
                    var links = JsonConvert.DeserializeObject<List<Core.Models.LinkInfo>>(str);
                    GridView.ItemsSource = links;
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

        }

        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            var info = image.DataContext as Core.Models.LinkInfo;
            if(!string.IsNullOrEmpty(info.IconUrl))
            {
                string url;
                if(info.IconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    url = info.IconUrl;
                }
                else
                {
                    url = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "LinkIcons", info.IconUrl);
                }
                image.Source = new BitmapImage(new Uri(url));
            }
        }
    }
}
