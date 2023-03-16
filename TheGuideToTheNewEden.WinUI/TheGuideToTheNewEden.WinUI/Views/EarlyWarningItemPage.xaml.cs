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
using System.Reflection;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class EarlyWarningItemPage : Page
    {
        private TabViewItem TabViewItem;
        public EarlyWarningItemPage(TabViewItem tabViewItem)
        {
            TabViewItem = tabViewItem;
            this.InitializeComponent();
            Loaded += EarlyWarningItemPage_Loaded;
        }

        private void EarlyWarningItemPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.OnSelectedCharacterChanged += EarlyWarningItemPage_OnSelectedCharacterChanged;
            ChatContents.Blocks.Add(new Paragraph());
            VM.ChatContents.CollectionChanged += ChatContents_CollectionChanged;
            ChatContentsScroll.LayoutUpdated += ChatContentsScroll_LayoutUpdated;
            VM.PropertyChanged += EarlyWarningItemPage_PropertyChanged;
        }

        private void EarlyWarningItemPage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(EarlyWarningItemViewModel.SelectedMapSolarSystem))
            {
                SetAutoSuggestBoxText(VM.SelectedMapSolarSystem?.SolarSystemName);
            }
            else if(e.PropertyName == nameof(EarlyWarningItemViewModel.SelectedNameDbs))
            {
                SetSelectedNameDbs();
            }
        }

        private void EarlyWarningItemPage_OnSelectedCharacterChanged(string selectedCharacter)
        {
            TabViewItem.Header = selectedCharacter;
        }

        private void ChatContentsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ChatContentsScroll.ScrollToVerticalOffset(ChatContentsScroll.ScrollableHeight);
        }
        private bool isAdded = false;
        private void ChatContents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    var chatContent = item as IntelChatContent;
                    Paragraph paragraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 8, 0, 8),
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
                    switch(chatContent.IntelType)
                    {
                        case Core.Enums.IntelChatType.Intel: contentRun.Foreground = new SolidColorBrush(Colors.OrangeRed);break;
                        case Core.Enums.IntelChatType.Clear: contentRun.Foreground = new SolidColorBrush(Colors.SeaGreen); break;
                    }
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
            else if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                ChatContents.Blocks.Clear();
            }
        }

        private void ChatContentsScroll_LayoutUpdated(object sender, object e)
        {
            if(isAdded)
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
        private bool IgnoreNameDbsSelectionChanged;
        private void GridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(!IgnoreNameDbsSelectionChanged)
            {
                VM.SelectedNameDbs = (sender as ListView).SelectedItems?.ToList<string>();
            }
        }
        private void SetSelectedNameDbs()
        {
            IgnoreNameDbsSelectionChanged = true;
            foreach (var item in VM.SelectedNameDbs)
            {
                SelectedNameDbsListView.SelectedItems.Add(item);
            }
            IgnoreNameDbsSelectionChanged = false;
        }
        #endregion
        private void ListView_Loaded(object sender, RoutedEventArgs e)
        {
            if(VM.SelectedNameDbs != null)
            {
                foreach (var item in VM.SelectedNameDbs)
                {
                    (sender as ListView).SelectedItems.Add(item);
                }
            }
        }

        #region 自动填充角色位置
        private bool textChangedIgnore = false;
        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if(textChangedIgnore)
            {
                textChangedIgnore = false;
                sender.IsSuggestionListOpen = false;
                return;
            }
            if(string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = VM.MapSolarSystems;
            }
            else
            {
                var targets = VM.MapSolarSystems.Where(p => p.SolarSystemName.Contains(sender.Text)).ToList();
                sender.ItemsSource = targets;
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            VM.SelectedMapSolarSystem = args.SelectedItem as Core.DBModels.MapSolarSystemBase;
            SetAutoSuggestBoxText((args.SelectedItem as Core.DBModels.MapSolarSystemBase).SolarSystemName);
        }
        private void SetAutoSuggestBoxText(string text)
        {
            textChangedIgnore = true;
            LocationBox.Text = text;
        }
        #endregion
    }
}
