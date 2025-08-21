using CommunityToolkit.Mvvm.ComponentModel;
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Interfaces;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.Core.Services.ObservableFileService;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelTranslationWinPage : Page, INotifyPropertyChanged
    {
        internal class ChannelResultDisplayModel: ObservableObject
        {
            public string Header { get; set; }
            public ObservableCollection<ChannelTranslationResult> Results { get; set; } = new ObservableCollection<ChannelTranslationResult>();
            private bool _unread = false;
            public bool Unread { get => _unread; set => SetProperty(ref _unread, value); }

            private int unreadCount;
            public int UnreadCount { get => unreadCount; set => SetProperty(ref unreadCount, value); }
        }

        private bool _showFrom;
        public bool ShowFrom
        {
            get => _showFrom;
            set
            {
                _showFrom = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFrom)));
            }
        }

        private IWindow _window;
        private ITranslationService _translationService;
        private ObservableCollection<ChannelResultDisplayModel> _results = new ObservableCollection<ChannelResultDisplayModel>();
        private Dictionary<string, ChannelResultDisplayModel> _resultsDict = new Dictionary<string, ChannelResultDisplayModel>();
        public ChannelTranslationWinPage()
        {
            Loaded += ChannelTranslationWinPage_Loaded;
            this.InitializeComponent();
            _translationService = ClientServiceHelper.GetRequiredService<ITranslationService>();
            ContentTabView.TabItemsSource = _results;
        }

        private void ChannelTranslationWinPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ChannelTranslationWinPage_Loaded;
            //ContentList.LayoutUpdated += ContentList_LayoutUpdated;
        }

        private void ContentList_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                //ContentList.ScrollIntoView(ContentList.Items.LastOrDefault());
            }
        }

        public void SetWindow(IWindow window)
        {
            _window = window;
        }
        private bool isAdded = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public async void UpdateContent(IEnumerable<Core.Models.EVELogs.ChatContent> marketChatContents, string from, string to)
        {
            foreach (var chatContent in marketChatContents)
            {
                isAdded = true;
                var result = await _translationService.Translate(chatContent.Content, from, to);
                ChannelResultDisplayModel resultDisplayModel = null;
                if (!_resultsDict.TryGetValue(chatContent.ChannelID, out resultDisplayModel))
                {
                    resultDisplayModel = new ChannelResultDisplayModel()
                    {
                        Header = chatContent.ChannelName,
                    };
                    _resultsDict.Add(chatContent.ChannelID, resultDisplayModel);
                    _results.Add(resultDisplayModel);
                }
                resultDisplayModel.Results.Add(new ChannelTranslationResult()
                {
                    ChatContent = chatContent,
                    TranslationResult = result
                });
                resultDisplayModel.Unread = true;
                resultDisplayModel.UnreadCount++;
            }
        }

        private async void ManualTranslateTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter)
            {
                string text = (sender as TextBox).Text;
                if (string.IsNullOrEmpty(text))
                {
                    ManualTranslateResultTextBlock.Text = string.Empty;
                }
                else
                {
                    var result = await _translationService.Translate(text, (FromComboBox.SelectedItem as ComboBoxItem).Content.ToString(), (ToComboBox.SelectedItem as ComboBoxItem).Content.ToString());
                    if (result.Success)
                    {
                        ManualTranslateResultTextBlock.Text = result.Result;
                    }
                    else
                    {
                        ManualTranslateResultTextBlock.Text = result.ErrorMsg;
                    }
                }
            }
        }

        private void ShowManualTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            ManualTranslateArea.Visibility = ManualTranslateArea.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ShowFrom = (sender as ToggleSwitch).IsOn;
        }

        private void ClearResultButton_Click(object sender, RoutedEventArgs e)
        {
            _results.Clear();
        }
        private void ResultsListView_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
