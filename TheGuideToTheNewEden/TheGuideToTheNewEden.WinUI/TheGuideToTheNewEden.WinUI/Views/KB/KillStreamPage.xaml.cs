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
            VM.Config.EnsureRoleFiltersInitialized();
            CommonExclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            });
            CommonExclusionsIdNameSearchBox.OnSelectedItemChanged += CommonExclusionsIdNameSearchBox_OnSelectedItemChanged;

            CommonInclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.SolarSystem,
                Core.DBModels.IdName.CategoryEnum.Region,
                Core.DBModels.IdName.CategoryEnum.InventoryType,
            });
            CommonInclusionsIdNameSearchBox.OnSelectedItemChanged += CommonInclusionsIdNameSearchBox_OnSelectedItemChanged;

            VictimExclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
            });
            VictimExclusionsIdNameSearchBox.OnSelectedItemChanged += VictimExclusionsIdNameSearchBox_OnSelectedItemChanged;

            VictimInclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
            });
            VictimInclusionsIdNameSearchBox.OnSelectedItemChanged += VictimInclusionsIdNameSearchBox_OnSelectedItemChanged;

            AttackerExclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
            });
            AttackerExclusionsIdNameSearchBox.OnSelectedItemChanged += AttackerExclusionsIdNameSearchBox_OnSelectedItemChanged;

            AttackerInclusionsIdNameSearchBox.SetFilterTypes(new List<Core.DBModels.IdName.CategoryEnum>()
            {
                Core.DBModels.IdName.CategoryEnum.Character,
                Core.DBModels.IdName.CategoryEnum.Corporation,
                Core.DBModels.IdName.CategoryEnum.Alliance,
            });
            AttackerInclusionsIdNameSearchBox.OnSelectedItemChanged += AttackerInclusionsIdNameSearchBox_OnSelectedItemChanged;
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
            GeneralSetting.Visibility = Visibility.Collapsed;
            FilterSetting.Visibility = Visibility.Collapsed;
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag.ToString())
                {
                    case "General": GeneralSetting.Visibility = Visibility.Visible; break;
                    case "Filter": FilterSetting.Visibility = Visibility.Visible;break;
                }
            }
        }
        private void FilterGroup_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            CommonFilterPanel.Visibility = Visibility.Collapsed;
            VictimFilterPanel.Visibility = Visibility.Collapsed;
            AttackerFilterPanel.Visibility = Visibility.Collapsed;
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag?.ToString())
                {
                    case "Common":
                        CommonFilterPanel.Visibility = Visibility.Visible;
                        break;
                    case "Victim":
                        VictimFilterPanel.Visibility = Visibility.Visible;
                        break;
                    case "Attacker":
                        AttackerFilterPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        #region
        private void CommonExclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, IdName e)
        {
            VM.Config.CommonExclusions.Add(e);
        }

        private void DeleteCommonExclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as IdName;
            if (data != null)
            {
                VM.Config.CommonExclusions.Remove(data);
            }
        }

        private void CommonInclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, IdName e)
        {
            VM.Config.CommonInclusions.Add(e);
        }

        private void DeleteCommonInclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as IdName;
            if (data != null)
            {
                VM.Config.CommonInclusions.Remove(data);
            }
        }

        private void VictimExclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.VictimExclusions.Add(e);
        }

        private void DeleteVictimExclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if (data != null)
            {
                VM.Config.VictimExclusions.Remove(data);
            }
        }
        private void VictimInclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, Core.DBModels.IdName e)
        {
            VM.Config.VictimInclusions.Add(e);
        }

        private void DeleteVictimInclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as Core.DBModels.IdName;
            if (data != null)
            {
                VM.Config.VictimInclusions.Remove(data);
            }
        }

        private void AttackerExclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, IdName e)
        {
            VM.Config.AttackerExclusions.Add(e);
        }

        private void DeleteAttackerExclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as IdName;
            if (data != null)
            {
                VM.Config.AttackerExclusions.Remove(data);
            }
        }

        private void AttackerInclusionsIdNameSearchBox_OnSelectedItemChanged(object sender, IdName e)
        {
            VM.Config.AttackerInclusions.Add(e);
        }

        private void DeleteAttackerInclusions_Click(object sender, RoutedEventArgs e)
        {
            var data = (sender as Button)?.DataContext as IdName;
            if (data != null)
            {
                VM.Config.AttackerInclusions.Remove(data);
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
        }

        private void CloseSettingButton_Click(object sender, RoutedEventArgs e)
        {
            SettingGrid.Visibility = Visibility.Collapsed;
            if (VM.TotalReceivedCount > 0)
            {
                DisconnectGrid.Visibility = Visibility.Collapsed;
                ConnectedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                DisconnectGrid.Visibility = Visibility.Visible;
                ConnectedGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void KBListTab_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem != null)
            {
                switch (sender.SelectedItem.Tag?.ToString())
                {
                    case "Matched": MatchedKBListControl.Visibility = Visibility.Visible; FilteredOutKBListControl.Visibility = Visibility.Collapsed; break;
                    case "FilteredOut": MatchedKBListControl.Visibility = Visibility.Collapsed; FilteredOutKBListControl.Visibility = Visibility.Visible; break;
                    default:break;
                }
            }
        }
    }
}
