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

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class StatistKBListPage : Page
    {
        private readonly Services.KBNavigationService _kbNavigationService;
        private readonly EntityStatistic _statistic;
        private Window _window;
        public StatistKBListPage(EntityStatistic statistic, KBNavigationService kbNavigationService)
        {
            _kbNavigationService = kbNavigationService;
            _statistic = statistic;
            this.InitializeComponent();
            Loaded += StatistOverviewPage_Loaded;
            KBListControl.SetKBNavigationService(kbNavigationService);
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

        //private void KBListControl_OnItemClicked(Core.Models.KB.KBItemInfo itemInfo)
        //{
        //    _kbNavigationService.NavigateToKM(itemInfo);
        //}

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
    }
}
