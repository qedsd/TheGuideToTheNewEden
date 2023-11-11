using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class GameLogMonitorPage : Page, IPage
    {
        private Window _window;
        public GameLogMonitorPage()
        {
            this.InitializeComponent();
            VM.OnContentUpdate += VM_OnContentUpdate;
            GameLogContentsScroll.LayoutUpdated += GameLogContentsScroll_LayoutUpdated;
            Loaded += GameLogMonitorPage_Loaded;
            VM.PropertyChanged += VM_PropertyChanged;
        }

        private void VM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(VM.SelectedGameLogInfo))
            {
                GameLogContents.Blocks.Clear();
                if (VM.SelectedGameLogInfo?.LogContents.NotNullOrEmpty() == true)
                {
                    AddContentsToUI(VM.SelectedGameLogInfo.LogContents);
                }
            }
        }

        private void GameLogMonitorPage_Loaded(object sender, RoutedEventArgs e)
        {
            _window = Helpers.WindowHelper.GetWindowForElement(this);
        }

        private void GameLogContentsScroll_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                GameLogContentsScroll.ScrollToVerticalOffset(GameLogContentsScroll.ScrollableHeight);
            }
        }
        private bool isAdded = false;

        private void VM_OnContentUpdate(Core.Models.GameLogItem item, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                AddContentsToUI(news);
            });
        }

        private void AddContentsToUI(IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            var lastParagraph = GameLogContents.Blocks.LastOrDefault() as Paragraph;
            if (lastParagraph != null)
            {
                foreach (var run in lastParagraph.Inlines)
                {
                    run.FontWeight = FontWeights.Normal;
                }
            }
            Run lastRun = null;
            foreach (var item2 in news)
            {
                Paragraph paragraph = new Paragraph()
                {
                    Margin = new Thickness(0, 16, 0, 16),
                };
                Run contentRun = new Run()
                {
                    Text = item2.SourceContent
                };
                if (item2.Important)
                {
                    contentRun.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
                }
                paragraph.Inlines.Add(contentRun);
                GameLogContents.Blocks.Add(paragraph);
                lastRun = contentRun;
            }
            if(lastRun != null)
            {
                lastRun.FontWeight = FontWeights.Bold;
            }
            isAdded = true;
        }

        public void Close()
        {
            VM.Dispose();
        }


        private void MenuFlyoutItem_LogFile_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as GameLogInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }

        private void Button_DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            VM.GameLogSetting.Keys.Remove((sender as Button).DataContext as GameLogMonityKey);
        }
        private void Button_DeleteThreadErrorKey_Click(object sender, RoutedEventArgs e)
        {
            VM.GameLogSetting.ThreadErrorKeys.Remove((sender as Button).DataContext as GameLogMonityKey);
        }
    }
}
