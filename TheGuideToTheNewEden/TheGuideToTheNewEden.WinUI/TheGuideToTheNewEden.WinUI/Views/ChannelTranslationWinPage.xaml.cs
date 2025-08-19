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
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.Core.Services.ObservableFileService;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelTranslationWinPage : Page
    {
        private IWindow _window;
        private ITranslationService _translationService;
        public ChannelTranslationWinPage()
        {
            Loaded += ChannelTranslationWinPage_Loaded;
            this.InitializeComponent();
            _translationService = ClientServiceHelper.GetRequiredService<ITranslationService>();
        }

        private void ChannelTranslationWinPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ChannelTranslationWinPage_Loaded;
            ChatContentsScroll.LayoutUpdated += ChatContentsScroll_LayoutUpdated;
        }

        private void ChatContentsScroll_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                ChatContentsScroll.ScrollToVerticalOffset(ChatContentsScroll.ScrollableHeight);
            }
        }

        public void SetWindow(IWindow window)
        {
            _window = window;
        }
        private bool isAdded = false;
        public async void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> marketChatContents, string from, string to)
        {
            foreach (var chatContent in marketChatContents)
            {
                var result = await _translationService.Translate(chatContent.Content, from, to);
                ChannelTranslationResult translationResult = new ChannelTranslationResult()
                {
                    ChatContent = chatContent,
                    TranslationResult = result
                };
                Paragraph paragraph1 = new Paragraph()
                {
                    Margin = new Thickness(0, 8, 0, 0),
                };
                Paragraph paragraph2 = new Paragraph()
                {
                    Margin = new Thickness(0, 0, 0, 8),
                };

                Run listener = new Run()
                {
                    FontWeight = FontWeights.Bold,
                    Text = $"{chatContent.ChannelName} : "
                };
                Run timeRun = new Run()
                {
                    FontWeight = FontWeights.Light,
                    Text = $"[ {chatContent.EVETime} ]"
                };
                Run nameRun = new Run()
                {
                    FontWeight = FontWeights.Thin,
                    Text = $" {chatContent.SpeakerName} > "
                };
                Run queryRun = new Run()
                {
                    FontWeight = FontWeights.Normal,
                    Text = $" {result.Query}"
                };
                Run translteRun = new Run()
                {
                    FontWeight = FontWeights.Normal,
                    Text = $"{result.Result}"
                };
                Run fromToRun = new Run()
                {
                    FontWeight = FontWeights.Thin,
                    Text = $" {result.From} -> {result.To}"
                };
                paragraph1.Inlines.Add(listener);
                paragraph1.Inlines.Add(timeRun);
                paragraph1.Inlines.Add(nameRun);
                paragraph1.Inlines.Add(queryRun);
                paragraph2.Inlines.Add(translteRun);
                paragraph2.Inlines.Add(fromToRun);
                if (ChatContentsScroll.VerticalOffset == ChatContentsScroll.ScrollableHeight)
                {
                    isAdded = true;
                }
                ChatContents.Blocks.Add(paragraph1);
                ChatContents.Blocks.Add(paragraph2);
            }
        }

        private void MenuFlyoutItem_ClearNews_Click(object sender, RoutedEventArgs e)
        {
            ChatContents.Blocks.Clear();
        }
    }
}
