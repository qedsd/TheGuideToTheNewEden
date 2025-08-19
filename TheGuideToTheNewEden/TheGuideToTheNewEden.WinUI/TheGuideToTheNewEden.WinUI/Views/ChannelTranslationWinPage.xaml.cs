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
using System.Collections.ObjectModel;
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
        private ObservableCollection<ChannelTranslationResult> _results = new ObservableCollection<ChannelTranslationResult>();
        public ChannelTranslationWinPage()
        {
            Loaded += ChannelTranslationWinPage_Loaded;
            this.InitializeComponent();
            _translationService = ClientServiceHelper.GetRequiredService<ITranslationService>();
            ContentList.ItemsSource = _results;
        }

        private void ChannelTranslationWinPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ChannelTranslationWinPage_Loaded;
            ContentList.LayoutUpdated += ContentList_LayoutUpdated;
        }

        private void ContentList_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                ContentList.ScrollIntoView(ContentList.Items.LastOrDefault());
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
                isAdded = true;
                var result = await _translationService.Translate(chatContent.Content, from, to);
                _results.Add(new ChannelTranslationResult()
                {
                    ChatContent = chatContent,
                    TranslationResult = result
                });
            }
        }

        private void MenuFlyoutItem_ClearNews_Click(object sender, RoutedEventArgs e)
        {
            //ChatContents.Blocks.Clear();
        }
    }
}
