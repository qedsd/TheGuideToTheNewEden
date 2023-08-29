using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
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
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Foundation.Collections;
using Windows.Globalization.NumberFormatting;
using Windows.UI;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class LocalIntelPage : Page, IPage
    {
        public LocalIntelPage()
        {
            this.InitializeComponent();
            Loaded += LocalIntelPage_Loaded;
        }

        private void LocalIntelPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= LocalIntelPage_Loaded;
            SetNumberBoxNumberFormatter();
        }
        private void SetNumberBoxNumberFormatter()
        {
            IncrementNumberRounder rounder = new IncrementNumberRounder();
            rounder.Increment = 0.01;
            rounder.RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp;

            DecimalFormatter formatter = new DecimalFormatter();
            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 2;
            formatter.NumberRounder = rounder;
            NumberBox_FillThresholdV.NumberFormatter = formatter;
            NumberBox_FillThresholdH.NumberFormatter = formatter;
        }

        private void Button_RemoveStanding_Click(object sender, RoutedEventArgs e)
        {
            VM.RemoveStanding((sender as Button).DataContext as LocalIntelStandingSetting);
        }

        public void Close()
        {
            VM.Dispose();
        }
    }
}
