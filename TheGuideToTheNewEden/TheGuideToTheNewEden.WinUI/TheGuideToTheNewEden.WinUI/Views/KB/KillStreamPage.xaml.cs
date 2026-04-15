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
        private bool _autoConnect;
        public KillStreamPage(bool autoConnect = true)
        {
            _autoConnect = autoConnect;
            _kbNavigationService = ClientServiceHelper.GetRequiredService<KBNavigationService>();
            this.InitializeComponent();
            Loaded += KillStreamPage_Loaded;
        }

        private void KillStreamPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= KillStreamPage_Loaded;
            ExclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            });
            ExclusionsIdNameSearchBox.OnSelectedItemChanged += ExclusionsIdNameSearchBox_OnSelectedItemChanged;

            InclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            });
            InclusionsIdNameSearchBox.OnSelectedItemChanged += InclusionsIdNameSearchBox_OnSelectedItemChanged;
            if (_autoConnect && Services.Settings.ZKBSettingService.Setting.AutoConnect)
            {
                Connect();
                DisconnectGrid.Visibility = Visibility.Collapsed;
                ConnectedGrid.Visibility = Visibility.Visible;
                SettingGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                DisconnectGrid.Visibility = Visibility.Visible;
                ConnectedGrid.Visibility = Visibility.Collapsed;
                SettingGrid.Visibility = Visibility.Collapsed;
            }
        }
        private async void Connect()
        {
            this.ShowWaiting(Helpers.ResourcesHelper.GetString("ZKBHomePage_ConnectingToWSS"));
            DisconnectGrid.Visibility = Visibility.Collapsed;
            ConnectedGrid.Visibility = Visibility.Collapsed;
            SettingGrid.Visibility = Visibility.Collapsed;
            if(await VM.InitAsync())
            {
                Button_Connect.Visibility = Visibility.Collapsed;
                Button_Disconnect.Visibility = Visibility.Visible;
                Button_Setting.Visibility = Visibility.Collapsed;
                ConnectedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                DisconnectGrid.Visibility = Visibility.Visible;
            }
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

        private void Setting_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            FilterSetting.Visibility = Visibility.Collapsed;
            NotifySetting.Visibility = Visibility.Collapsed;
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag.ToString())
                {
                    case "0": FilterSetting.Visibility = Visibility.Visible; break;
                    case "1": NotifySetting.Visibility = Visibility.Visible;break;
                }
            }
        }

        #region ¹ýÂËÁÐ±í
        private void ExclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.Exclusions.Add(e);
        }

        private void DeleteExclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if (data != null)
            {
                VM.Config.Exclusions.Remove(data);
            }
        }
        private void InclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.Inclusions.Add(e);
        }

        private void DeleteInclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if (data != null)
            {
                VM.Config.Inclusions.Remove(data);
            }
        }
        #endregion

        private void Button_Setting_Click(object sender, RoutedEventArgs e)
        {
            DisconnectGrid.Visibility = Visibility.Collapsed;
            ConnectedGrid.Visibility = Visibility.Collapsed;
            SettingGrid.Visibility = Visibility.Visible;
        }

        private void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            VM.Stop();
            Button_Connect.Visibility = Visibility.Visible;
            Button_Disconnect.Visibility = Visibility.Collapsed;
            Button_Setting.Visibility = Visibility.Visible;
            ConnectedGrid.Visibility = Visibility.Collapsed;
            DisconnectGrid.Visibility = Visibility.Visible;
        }
    }
}
