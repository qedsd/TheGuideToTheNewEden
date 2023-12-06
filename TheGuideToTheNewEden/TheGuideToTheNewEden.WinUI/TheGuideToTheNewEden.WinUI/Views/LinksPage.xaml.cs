// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TheGuideToTheNewEden.WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LinksPage : Page
    {
        public LinksPage()
        {
            this.InitializeComponent();
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
                    DataGrid.ItemsSource = links;
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
        private void Copy_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
