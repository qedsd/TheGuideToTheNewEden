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
using Syncfusion.UI.Xaml.Grids.ScrollAxis;
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
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Interfaces;
using TheGuideToTheNewEden.Core.Extensions;
using CommunityToolkit.WinUI.UI;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelTranslationWinPage : Page, INotifyPropertyChanged
    {
        internal class ChannelResultDisplayModel: ObservableObject
        {
            public string ChannelID {  get; set; }
            public string Listener { get; set; }
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
            this.InitializeComponent();
            _translationService = ClientServiceHelper.GetRequiredService<ITranslationService>();
            ContentTabView.TabItemsSource = _results;
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
                        Listener = chatContent.Listener,
                        ChannelID = chatContent.ChannelID
                    };
                    _resultsDict.Add(chatContent.ChannelID, resultDisplayModel);
                    _results.Add(resultDisplayModel);
                    if(_results.Count == 1)
                    {
                        ContentTabView.SelectedIndex = 0;
                    }
                }
                resultDisplayModel.Results.Add(new ChannelTranslationResult()
                {
                    ChatContent = chatContent,
                    TranslationResult = result
                });
                if(_listView?.DataContext == resultDisplayModel)
                {
                    if (_autoScroll)
                    {
                        ScrollToBottom();
                    }
                    else
                    {
                        resultDisplayModel.Unread = true;
                        resultDisplayModel.UnreadCount++;
                    }
                }
                else
                {
                    resultDisplayModel.Unread = true;
                    resultDisplayModel.UnreadCount++;
                }
            }
        }
        public void Remove(string listener)
        {
            var removed = _results.Where(p=>p.Listener == listener).ToList();
            if (removed.NotNullOrEmpty())
            {
                foreach(var item in removed)
                {
                    _results.Remove(item);
                    _resultsDict.Remove(item.ChannelID);
                }
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
            foreach(var result in _results)
            {
                result.Results.Clear();
                result.Unread = false;
                result.UnreadCount = 0;
            }
        }

        private bool _autoScroll = true;
        private bool _userScrolled = false;
        private ListView _listView;
        private void ResultsListView_Loaded(object sender, RoutedEventArgs e)
        {
            ListView listView = sender as ListView;
            _listView = listView;
            listView.Unloaded += ListView_Unloaded;
            if (listView.FindDescendant<ScrollViewer>() is ScrollViewer scrollViewer)
            {
                scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
            }
            var item = (sender as ListView).DataContext as ChannelResultDisplayModel;
            item.Unread = false;
            item.UnreadCount = 0;
            ScrollToBottom();
        }
        private void ScrollToBottom()
        {
            if (_listView?.Items.Count > 0)
            {
                // 使用Dispatcher确保在UI线程上执行
                DispatcherQueue.SafelyTryEnqueue(() =>
                {
                    var lastItem = _listView.Items.Last();
                    _listView.ScrollIntoView(lastItem);
                });
            }
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;

            var verticalOffset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;
            var viewportHeight = scrollViewer.ViewportHeight;

            // 用户滚动到最底部时恢复自动滚动
            if (verticalOffset >= scrollableHeight - 1) // 使用容差
            {
                _userScrolled = false;
                _autoScroll = true;
                if(_listView?.DataContext is ChannelResultDisplayModel model)
                {
                    model.Unread = false;
                    model.UnreadCount = 0;
                }
                
            }
            // 用户向上滚动时暂停自动滚动
            else if (!e.IsIntermediate) // 只有当滚动结束时才判断
            {
                _userScrolled = true;
                _autoScroll = false;
            }
        }

        private void ListView_Unloaded(object sender, RoutedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView.FindDescendant<ScrollViewer>() is ScrollViewer scrollViewer)
            {
                scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
            }
        }

        private void ListenerButton_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName((sender as Button).Content.ToString());
            if (hwnd > 0)
            {
                Helpers.WindowHelper.SetForegroundWindow1(hwnd);
            }
        }
    }
}
