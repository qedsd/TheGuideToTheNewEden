﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace TheGuideToTheNewEden.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class CharacterPage : Page
    {
        public ViewModels.CharacterViewModel ViewModel { get; } = new ViewModels.CharacterViewModel();
        public CharacterPage()
        {
            this.InitializeComponent();
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ListBox_MailList_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void ListBox_Implant_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void ListBox_Contract_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void ListBox_CHMails_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void ListBox_Label_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        
    }
}
