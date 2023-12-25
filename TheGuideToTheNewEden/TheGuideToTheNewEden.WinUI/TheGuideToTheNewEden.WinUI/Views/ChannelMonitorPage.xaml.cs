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
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelMonitorPage : Page
    {
        private Window _window;
        public ChannelMonitorPage()
        {
            this.InitializeComponent();
            Loaded += ChannelMonitorPage_Loaded;
            GameLogContentsScroll.LayoutUpdated += GameLogContentsScroll_LayoutUpdated;
            VM.OnContentUpdate += VM_OnContentUpdate;
        }

        private void ChannelMonitorPage_Loaded(object sender, RoutedEventArgs e)
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
        private void VM_OnContentUpdate(string name, IEnumerable<Core.Models.EVELogs.ChatContent> chatContents)
        {
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                AddContentsToUI(name, chatContents);
            });
        }
        private Run _lastRun;
        private void AddContentsToUI(string name, IEnumerable<Core.Models.EVELogs.ChatContent> news)
        {
            var importants = news.Where(p => p.Important).ToList();
            if(importants.NotNullOrEmpty())
            {
                if (_lastRun != null)
                {
                    _lastRun.FontWeight = FontWeights.Normal;
                }
                foreach (var chatContent in importants)
                {
                    Paragraph paragraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 16, 0, 16),
                    };
                    Run characterRun = new Run()
                    {
                        FontWeight = FontWeights.SemiBold,
                        Text = $"<{name}> "
                    };
                    Run timeRun = new Run()
                    {
                        FontWeight = FontWeights.Light,
                        Text = $"[ {chatContent.EVETime} ]"
                    };
                    Run nameRun = new Run()
                    {
                        FontWeight = FontWeights.Normal,
                        Text = $" {chatContent.SpeakerName} > "
                    };
                    Run contentRun = new Run()
                    {
                        FontWeight = FontWeights.Normal,
                        Text = chatContent.Content
                    };
                    paragraph.Inlines.Add(characterRun);
                    paragraph.Inlines.Add(timeRun);
                    paragraph.Inlines.Add(nameRun);
                    paragraph.Inlines.Add(contentRun);
                    GameLogContents.Blocks.Add(paragraph);
                    _lastRun = contentRun;
                }
                if (_lastRun != null)
                {
                    _lastRun.FontWeight = FontWeights.Bold;
                }
                isAdded = true;
            }
        }
        private void MenuFlyoutItem_OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }

        private void Button_DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            VM.SelectedCharacter.Setting.Keys.Remove((sender as Button).DataContext as ChannelMonitorKey);
        }

        private void MenuFlyoutItem_ClearNews_Click(object sender, RoutedEventArgs e)
        {
            GameLogContents.Blocks.Clear();
        }
    }
}
