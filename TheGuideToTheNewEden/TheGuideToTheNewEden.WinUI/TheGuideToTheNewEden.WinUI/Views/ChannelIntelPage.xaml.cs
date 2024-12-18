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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelIntelPage : Page,IPage
    {
        private static int _runningPageCount = 0;
        public ChannelIntelPage()
        {
            _runningPageCount++;
            this.InitializeComponent();
            Loaded += ChannelIntelPage_Loaded;
        }

        private void ChannelIntelPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ChannelIntelPage_Loaded;
            ChatContents.Blocks.Add(new Paragraph());
            VM.ChatContents.CollectionChanged += ChatContents_CollectionChanged;
            VM.ZKBIntelContents.CollectionChanged += ZKBIntelContents_CollectionChanged;
            ChatContentsScroll.LayoutUpdated += ChatContentsScroll_LayoutUpdated;
            VM.PropertyChanged += ChannelIntelPage_PropertyChanged;
        }

        private void ZKBIntelContents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                //删除超出显示数量的
                if (Services.Settings.GameLogsSettingService.MaxShowItems > 0)
                {
                    int removeCount = ChatContents.Blocks.Count - Services.Settings.GameLogsSettingService.MaxShowItems + e.NewItems.Count;
                    for (int i = 0; i < removeCount; i++)
                    {
                        ChatContents.Blocks.RemoveAt(0);
                    }
                }
                foreach (var item in e.NewItems)
                {
                    var chatContent = item as Core.Models.EarlyWarningContent;
                    Paragraph paragraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 8, 0, 8),
                    };
                    Run timeRun = new Run()
                    {
                        FontWeight = FontWeights.Light,
                        Text = $"[ {chatContent.Time} ]"
                    };
                    Run nameRun = new Run()
                    {
                        FontWeight = FontWeights.Medium,
                        Text = " ZKB > "
                    };
                    Run contentRun = new Run()
                    {
                        FontWeight = FontWeights.Normal,
                        Text = chatContent.Content
                    };
                    contentRun.Foreground = new SolidColorBrush(Colors.OrangeRed);
                    paragraph.Inlines.Add(timeRun);
                    paragraph.Inlines.Add(nameRun);
                    paragraph.Inlines.Add(contentRun);
                    if (ChatContentsScroll.VerticalOffset == ChatContentsScroll.ScrollableHeight)
                    {
                        isAdded = true;
                    }
                    ChatContents.Blocks.Add(paragraph);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                ChatContents.Blocks.Clear();
            }
        }

        private void ChannelIntelPage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VM.ChannelIntel.SelectedNameDbs) || e.PropertyName == nameof(VM.ChannelIntel))
            {
                SetSelectedNameDbs();
            }
        }

        private void ChatContentsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ChatContentsScroll.ScrollToVerticalOffset(ChatContentsScroll.ScrollableHeight);
        }
        private bool isAdded = false;
        private void ChatContents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                //删除超出显示数量的
                if (Services.Settings.GameLogsSettingService.MaxShowItems > 0)
                {
                    int removeCount = ChatContents.Blocks.Count - Services.Settings.GameLogsSettingService.MaxShowItems + e.NewItems.Count;
                    for (int i = 0; i < removeCount; i++)
                    {
                        ChatContents.Blocks.RemoveAt(0);
                    }
                }
                foreach (var item in e.NewItems)
                {
                    var chatContent = item as IntelChatContent;
                    Paragraph paragraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 8, 0, 8),
                    };

                    Run listener = new Run()
                    {
                        FontWeight = FontWeights.Bold,
                        Text = $"{chatContent.Listener} : "
                    };
                    Run timeRun = new Run()
                    {
                        FontWeight = FontWeights.Light,
                        Text = $"[ {chatContent.EVETime} ]"
                    };
                    Run nameRun = new Run()
                    {
                        FontWeight = FontWeights.Medium,
                        Text = $" {chatContent.SpeakerName} > "
                    };
                    Run contentRun = new Run()
                    {
                        FontWeight = FontWeights.Normal,
                        Text = chatContent.Content
                    };
                    switch (chatContent.IntelType)
                    {
                        case Core.Enums.IntelChatType.Intel: contentRun.Foreground = new SolidColorBrush(Colors.OrangeRed); break;
                        case Core.Enums.IntelChatType.Clear: contentRun.Foreground = new SolidColorBrush(Colors.SeaGreen); break;
                    }
                    paragraph.Inlines.Add(listener);
                    paragraph.Inlines.Add(timeRun);
                    paragraph.Inlines.Add(nameRun);
                    paragraph.Inlines.Add(contentRun);
                    if (ChatContentsScroll.VerticalOffset == ChatContentsScroll.ScrollableHeight)
                    {
                        isAdded = true;
                    }
                    ChatContents.Blocks.Add(paragraph);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                ChatContents.Blocks.Clear();
            }
        }

        private void ChatContentsScroll_LayoutUpdated(object sender, object e)
        {
            if (isAdded)
            {
                isAdded = false;
                ChatContentsScroll.ScrollToVerticalOffset(ChatContentsScroll.ScrollableHeight);
            }
        }

        public void Stop()
        {
            VM.StopCommand.Execute(null);
        }

        #region 星系名语言数据库
        private bool _ignoreNameDbsSelectionChanged;
        private void GridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ignoreNameDbsSelectionChanged)
            {
                VM.ChannelIntel.SelectedNameDbs = (sender as ListView).SelectedItems?.ToList<string>();
            }
        }
        private void SetSelectedNameDbs()
        {
            _ignoreNameDbsSelectionChanged = true;
            SelectedNameDbsListView.SelectedItems.Clear();
            if(VM.ChannelIntel != null)
            {
                foreach (var item in VM.ChannelIntel.SelectedNameDbs)
                {
                    SelectedNameDbsListView.SelectedItems.Add(item);
                }
            }
            _ignoreNameDbsSelectionChanged = false;
        }
        #endregion
        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as ListView).Loaded -= ListView_Loaded;
            if (VM.ChannelIntel.SelectedNameDbs != null)
            {
                foreach (var item in VM.ChannelIntel.SelectedNameDbs)
                {
                    (sender as ListView).SelectedItems.Add(item);
                }
            }
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if (info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }

        private async void Button_PickSoundFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = await Helpers.PickHelper.PickFileAsync(Helpers.WindowHelper.GetWindowForElement(this));
                if (file != null)
                {
                    ((sender as FrameworkElement).Tag as TextBox).Text = file.Path;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                (Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow).ShowError(ex.Message);
            }
        }
        public void Close()
        {
            _runningPageCount--;
            VM.StopAllCommand.Execute(null);
            if (_runningPageCount == 0)
            {
                Core.Services.DB.MapSolarSystemNameService.ClearCache();
                Services.WarningService.Current.Dispose();
            }
        }

        private void MenuFlyoutItem_ClearNews_Click(object sender, RoutedEventArgs e)
        {
            ChatContents.Blocks.Clear();
        }
    }
}
