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
using TheGuideToTheNewEden.WinUI.Services;
using ZKB.NET.Models.Statistics;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class StatistTopAllTimePage : Page
    {
        private readonly Services.KBNavigationService _kbNavigationService;
        private readonly EntityStatistic _statistic;
        private Window _window;
        public StatistTopAllTimePage(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            _kbNavigationService = kbNavigationService;
            _statistic = statistic;
            this.InitializeComponent();
            Loaded += StatistOverviewPage_Loaded;
        }

        private async void StatistOverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StatistOverviewPage_Loaded;
            _window = this.GetWindow();
            VM.SetData(_statistic, _kbNavigationService);
            VM.SetWaitingAction(
                () => ShowWaiting(),
                () => HideWaiting());
            await VM.InitAsync();
        }

        private void ShowWaiting()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                ProgressGrid.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;
            });
        }
        private void HideWaiting()
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                ProgressGrid.Visibility = Visibility.Collapsed;
                ProgressRing.IsActive = false;
            });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as KillDataInfo;
            if (info != null)
            {
                this.GetBaseWindow()?.ShowWaiting();
                IdName.CategoryEnum? categoryEnum;
                switch(info.Type)
                {
                    case "character":categoryEnum = IdName.CategoryEnum.Character;break;
                    case "corporation": categoryEnum = IdName.CategoryEnum.Corporation; break;
                    case "alliance": categoryEnum = IdName.CategoryEnum.Alliance; break;
                    case "faction": categoryEnum = IdName.CategoryEnum.Faction; break;
                    case "ship": categoryEnum = IdName.CategoryEnum.InventoryType; break;
                    case "system": categoryEnum = IdName.CategoryEnum.SolarSystem; break;
                    default:
                        {
                            Core.Log.Error($"Unknown type:{info.Type}");
                            return;
                        }
                }
                await _kbNavigationService.NavigationTo(new IdName()
                {
                    Id = info.Id,
                    Name = info.Name,
                    Category = (int)categoryEnum
                });
                this.GetBaseWindow()?.HideWaiting();
            }
        }
    }
}
