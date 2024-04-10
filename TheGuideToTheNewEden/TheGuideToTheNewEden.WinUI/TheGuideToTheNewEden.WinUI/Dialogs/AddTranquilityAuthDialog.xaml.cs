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
using ABI.System;
using System.Diagnostics;
using TheGuideToTheNewEden.WinUI.Helpers;
using System.Threading;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class AddTranquilityAuthDialog : Page
    {
        public ESI.NET.Models.SSO.AuthorizedCharacterData AuthorizedCharacterData { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        public AddTranquilityAuthDialog()
        {
            this.InitializeComponent();
        }
        public static async Task<ESI.NET.Models.SSO.AuthorizedCharacterData> ShowAsync(XamlRoot xamlRoot)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = Helpers.ResourcesHelper.GetString("AddTranquilityAuthDialog_Title"),
                Content = new AddTranquilityAuthDialog(),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_Close")
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                (contentDialog.Content as AddTranquilityAuthDialog).Cancel();
                return (contentDialog.Content as AddTranquilityAuthDialog).AuthorizedCharacterData;
            }
            else
            {
                (contentDialog.Content as AddTranquilityAuthDialog).Cancel();
                return null;
            }
        }
        private void Button_Step0_Click(object sender, RoutedEventArgs e)
        {
            var sInfo = new ProcessStartInfo("https://dotnet.microsoft.com/zh-cn/download/dotnet/6.0")
            {
                UseShellExecute = true,
            };
            Process.Start(sInfo);
        }
        private string _lastAuthMsg = string.Empty;
        private async void Button_Step1_Click(object sender, RoutedEventArgs e)
        {
            Services.CharacterService.GetAuthorizeByBrower();
            _lastAuthMsg = string.Empty;
            Grid_WaitingStep2.Visibility = Visibility.Visible;
            ProgressRing_WaitingStep2.IsActive = true;
            Grid_Step2Result_Success.Visibility = Visibility.Collapsed;
            Grid_Step2Result_Failed.Visibility = Visibility.Collapsed;
            ProgressRing_WaitingStep3.IsActive = false;
            Grid_WaitingStep3.Visibility = Visibility.Collapsed;
            Grid_WaitingStep3_Success.Visibility = Visibility.Collapsed;
            Grid_WaitingStep3_Failed.Visibility = Visibility.Collapsed;

            _cancellationTokenSource = new CancellationTokenSource();
            string result = await AuthHelper.WaitingAuthAsync(_cancellationTokenSource.Token);
            Grid_WaitingStep2.Visibility = Visibility.Collapsed;
            ProgressRing_WaitingStep2.IsActive = false;
            if (result != null)
            {
                _lastAuthMsg = result;
                Grid_Step2Result_Success.Visibility = Visibility.Visible;
                StartVerify();
            }
            else
            {
                Grid_Step2Result_Failed.Visibility = Visibility.Visible;
            }
        }
        private async void StartVerify()
        {
            if (!string.IsNullOrEmpty(_lastAuthMsg))
            {
                ProgressRing_WaitingStep3.IsActive = true;
                Grid_WaitingStep3.Visibility = Visibility.Visible;
                Grid_WaitingStep3_Success.Visibility = Visibility.Collapsed;
                Grid_WaitingStep3_Failed.Visibility = Visibility.Collapsed;
                var result2 = await Services.CharacterService.HandelProtocolAsync(_lastAuthMsg);
                ProgressRing_WaitingStep3.IsActive = false;
                Grid_WaitingStep3.Visibility = Visibility.Collapsed;
                if (result2 != null)
                {
                    AuthorizedCharacterData = result2;
                    Grid_WaitingStep3_Success.Visibility = Visibility.Visible;
                }
                else//–£—È ß∞‹
                {
                    Grid_WaitingStep3_Failed.Visibility = Visibility.Visible;
                    TextBlock_Step3FailedDesc.Text = $"{Helpers.ResourcesHelper.GetString("AddTranquilityAuthDialog_VerifyFailed")} : {(Core.Log.GetLastError() as System.Exception).Message}";
                }
            }
        }


        private void Button_Step3_Click(object sender, RoutedEventArgs e)
        {
            StartVerify();
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
