// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using ABI.System;
using ESI.NET.Models.SSO;
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
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class AddSerenityAuthDialog : Page
    {
        public ESI.NET.Models.SSO.AuthorizedCharacterData AuthorizedCharacterData { get; private set; }
        public AddSerenityAuthDialog()
        {
            this.InitializeComponent();
        }
        public static async Task<ESI.NET.Models.SSO.AuthorizedCharacterData> ShowAsync(XamlRoot xamlRoot)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = Helpers.ResourcesHelper.GetString("AddSerenityAuthDialog_Title"),
                Content = new AddSerenityAuthDialog(),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_Close"),
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return (contentDialog.Content as AddSerenityAuthDialog).AuthorizedCharacterData;
            }
            else
            {
                return null;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CharacterService.GetAuthorizeByBrower();
        }

        private void Button_Step0_Click(object sender, RoutedEventArgs e)
        {
            var sInfo = new ProcessStartInfo(SerenityAuthHelper.LogoffUrl)
            {
                UseShellExecute = true,
            };
            Process.Start(sInfo);
        }

        private async void Button_Step3_Click(object sender, RoutedEventArgs e)
        {
            ProgressRing_WaitingStep3.IsActive = true;
            Grid_WaitingStep3.Visibility = Visibility.Visible;
            Grid_Step3_Success.Visibility = Visibility.Collapsed;
            Grid_Step3_FailedResult.Visibility = Visibility.Collapsed;
            Grid_Step3_Failed.Visibility = Visibility.Collapsed;
            var result2 = await Services.CharacterService.HandelProtocolAsync(TextBox_Code.Text);
            ProgressRing_WaitingStep3.IsActive = false;
            Grid_WaitingStep3.Visibility = Visibility.Collapsed;
            if (result2 != null)
            {
                AuthorizedCharacterData = result2;
                Grid_Step3_Success.Visibility = Visibility.Visible;
            }
            else//–£—È ß∞‹
            {
                Grid_Step3_FailedResult.Visibility = Visibility.Visible;
                Grid_Step3_Failed.Visibility = Visibility.Visible;
                TextBlock_Step3FailedDesc.Text = (Core.Log.GetLastError() as System.Exception).Message;
            }
        }
    }
}
