using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelTranslationWinPage : Page
    {
        private IWindow _window;
        private ITranslationService _translationService;
        public ChannelTranslationWinPage()
        {
            this.InitializeComponent();
            _translationService = ClientServiceHelper.GetRequiredService<ITranslationService>();
        }
        public void SetWindow(IWindow window)
        {
            _window = window;
        }
        public async void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> marketChatContents, string from, string to)
        {
            foreach (var chatContent in marketChatContents)
            {
                var result = await _translationService.Translate(chatContent.Content, from, to);
            }
        }

        private void MenuFlyoutItem_ClearNews_Click(object sender, RoutedEventArgs e)
        {
            ChatContents.Blocks.Clear();
        }
    }
}
