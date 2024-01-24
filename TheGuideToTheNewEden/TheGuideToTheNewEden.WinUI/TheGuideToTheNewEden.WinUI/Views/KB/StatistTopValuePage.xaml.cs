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
            VM.SetData(_statistic, _kbNavigationService);
            VM.SetWaitingAction(
                () => ShowWaiting(),
                () => HideWaiting());
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
    }
}
