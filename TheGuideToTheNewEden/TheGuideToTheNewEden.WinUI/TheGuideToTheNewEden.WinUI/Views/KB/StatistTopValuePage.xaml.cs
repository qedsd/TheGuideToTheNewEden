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
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class StatistTopValuePage : Page
    {
        private readonly Services.KBNavigationService _kbNavigationService;
        private readonly EntityStatistic _statistic;
        private Window _window;
        public StatistTopValuePage(EntityStatistic statistic, Services.KBNavigationService kbNavigationService)
        {
            _kbNavigationService = kbNavigationService;
            _statistic = statistic;
            this.InitializeComponent();
            Loaded += StatistTopValuePage_Loaded;
        }

        private void StatistTopValuePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StatistTopValuePage_Loaded;
            _window = this.GetWindow();
            VM.SetWaitingAction(
                () => ShowWaiting(),
                () => HideWaiting());
            VM.SetData(_statistic, _kbNavigationService);
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

        private async void KBTopKillControl_IdNameClicked(Core.DBModels.IdName idName)
        {
            this.GetBaseWindow()?.ShowWaiting();
            await _kbNavigationService.NavigationTo(idName);
            this.GetBaseWindow()?.HideWaiting();
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var info = e.ClickedItem as KBItemInfo;
            if(info != null)
            {
                _kbNavigationService.NavigateToKM(info);
            }
        }
    }
}
