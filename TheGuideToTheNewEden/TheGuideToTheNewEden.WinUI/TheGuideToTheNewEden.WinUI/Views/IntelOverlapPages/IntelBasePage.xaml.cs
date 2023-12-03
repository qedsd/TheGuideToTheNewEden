// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.IntelOverlapPages
{
    public sealed partial class IntelBasePage : Page
    {
        public IntelBasePage(IIntelOverlapPage intelOverlapPage)
        {
            this.InitializeComponent();
            MainFrame.Content = intelOverlapPage;
        }
        public event EventHandler OnIntelInfoButtonClicked;
        public event EventHandler OnStopSoundButtonClicked;
        public event EventHandler OnClearButtonClicked;

        public void SetIntelInfo(string info)
        {
            Button_IntelInfo.Content = info;
        }
        private void Button_IntelInfo_Click(object sender, RoutedEventArgs e)
        {
            OnIntelInfoButtonClicked?.Invoke(sender, EventArgs.Empty);
        }

        private void Button_StopSound_Click(object sender, RoutedEventArgs e)
        {
            OnStopSoundButtonClicked?.Invoke(sender, EventArgs.Empty);
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            OnClearButtonClicked?.Invoke(sender, EventArgs.Empty);
        }
    }
}
