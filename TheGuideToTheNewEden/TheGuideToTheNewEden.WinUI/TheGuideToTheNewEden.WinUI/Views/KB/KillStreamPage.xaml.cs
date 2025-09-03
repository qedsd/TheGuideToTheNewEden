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
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using System.Threading;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class KillStreamPage : Page
    {
        private Services.KBNavigationService _kbNavigationService;
        public KillStreamPage()
        {
            _kbNavigationService = ClientServiceHelper.GetRequiredService<KBNavigationService>();
            this.InitializeComponent();
            Loaded += KillStreamPage_Loaded;
        }

        private void KillStreamPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= KillStreamPage_Loaded;
            if (Services.Settings.ZKBSettingService.Setting.AutoConnect)
            {
                Connect();
            }
            else
            {
                KBListControl.Visibility = Visibility.Collapsed;
                Button_Connect.Visibility = Visibility.Visible;
            }
        }
        private async void Connect()
        {
            KBListControl.Visibility = Visibility.Visible;
            Button_Connect.Visibility = Visibility.Collapsed;
            this.ShowWaiting(Helpers.ResourcesHelper.GetString("ZKBHomePage_ConnectingToWSS"));
            await VM.InitAsync();
            this.HideWaiting();
        }
        private void KBListControl_OnItemClicked(Core.Models.KB.KBItemInfo itemInfo)
        {
            _kbNavigationService.NavigateToKM(itemInfo);
        }
        private void KBListControl_IdNameClicked(IdName idName)
        {
            ShowDetail(idName);
        }
        private async void ShowDetail(IdName idName)
        {
            try
            {
                this.ShowWaiting();
                await _kbNavigationService.NavigationTo(idName);
                this.HideWaiting();
            }
            catch (Exception e)
            {
                this.ShowMsg(e.Message, Controls.InfoBarControl.InfoType.Error, false);
                Core.Log.Error(e);
            }
        }
        public void Close()
        {
            VM?.Dispose();
        }
        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }
        private CancellationTokenSource _cancellationTokenSource;
    }
}
