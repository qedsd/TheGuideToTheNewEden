// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Notifications;
using Microsoft.Windows.AppNotifications;
using TheGuideToTheNewEden.WinUI.Dialogs;
using System.Text;


namespace TheGuideToTheNewEden.WinUI.Views.Settings
{
    public sealed partial class TestSettingPage : Page
    {
        public TestSettingPage()
        {
            this.InitializeComponent();
        }

        private void Button_SendToast_Click(object sender, RoutedEventArgs e)
        {
            TestToast.SendToast();
        }

        private void Button_CheckSetting_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder stringBuilder = new StringBuilder();
                var isSupported = AppNotificationManager.IsSupported();
                stringBuilder.Append(isSupported);
                stringBuilder.Append(" - ");
                var setting = AppNotificationManager.Default.Setting;
                string settingStr = string.Empty;
                switch(setting)
                {
                    case AppNotificationSetting.Enabled: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_Enabled"); break;
                    case AppNotificationSetting.DisabledForApplication: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_DisabledForApplication"); break;
                    case AppNotificationSetting.DisabledForUser: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_DisabledForUser"); break;
                    case AppNotificationSetting.DisabledByGroupPolicy: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_DisabledByGroupPolicy"); break;
                    case AppNotificationSetting.DisabledByManifest: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_DisabledByManifest"); break;
                    case AppNotificationSetting.Unsupported: settingStr = Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_Setting_Unsupported"); break;
                }
                stringBuilder.Append(settingStr);
                if(!isSupported || setting == AppNotificationSetting.Unsupported)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine(Helpers.ResourcesHelper.GetString("General_TryNotAdmin"));
                }
                ShowDialog(Helpers.ResourcesHelper.GetString("TestSettingPage_SystemNotify_CheckSettingResult"), stringBuilder.ToString());
            }
            catch(Exception ex)
            {
                ShowDialog("Error", ex.Message);
            }
        }

        private async void ShowDialog(string title, string msg)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = title,
                Content = new TextBlock() { Text = msg, TextWrapping = TextWrapping.Wrap},
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
            };
            await contentDialog.ShowAsync();
        }


        private Windows.Media.Playback.MediaPlayer _mediaPlayer;
        private string _soundFile;
        private void Button_MediaPlayer_Play_Click(object sender, RoutedEventArgs e)
        {
            if(_mediaPlayer == null)
            {
                _mediaPlayer = new Windows.Media.Playback.MediaPlayer();
            }
            _mediaPlayer.Pause();
            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(string.IsNullOrEmpty(_soundFile) ?
                    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3") :
                    _soundFile));
            _mediaPlayer.Play();
        }

        private void Button_MediaPlayer_Pause_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Pause();
        }

        private async void Button_MediaPlayer_Pick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await Helpers.PickHelper.PickFileAsync(Helpers.WindowHelper.GetWindowForElement(this));
                if(file != null)
                {
                    _soundFile = file.Path;
                }
            }
            catch(Exception ex)
            {
                ShowDialog("Error", ex.Message + "\n\r" + Helpers.ResourcesHelper.GetString("General_TryNotAdmin"));
            }
        }
    }
}
