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
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models;

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
            Loaded -= EarlyWarningItemPage_Loaded;
            VM.OnSelectedCharacterChanged += EarlyWarningItemPage_OnSelectedCharacterChanged;
            ChatContents.Blocks.Add(new Paragraph());
            VM.ChatContents.CollectionChanged += ChatContents_CollectionChanged;
            ChatContentsScroll.LayoutUpdated += ChatContentsScroll_LayoutUpdated;
            VM.PropertyChanged += EarlyWarningItemPage_PropertyChanged;
        }

        private void EarlyWarningItemPage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(EarlyWarningItemViewModel.SelectedNameDbs))
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

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as MenuFlyoutItem)?.DataContext as Models.ChatChanelInfo;
            if(info != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", info.FilePath);
            }
        }

        private void TextBox_SearchMapSolarSystem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty((sender as TextBox).Text))
            {
                VM.SearchMapSolarSystems = VM.MapSolarSystems;
            }
            else
            {
                VM.SearchMapSolarSystems = VM.MapSolarSystems.Where(p => p.SolarSystemName.Contains((sender as TextBox).Text)).ToList();
            }
        }

        private void NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if(!double.IsNaN(args.OldValue))
            {
                int diff = (int)(args.NewValue - args.OldValue);
                if (diff < 0)
                {
                    for (int i = 0; i < -diff; i++)
                    {
                        VM.Setting.Sounds.RemoveAt(VM.Setting.Sounds.Count - 1);
                    }
                }
                else
                {
                    for (int i = 0; i < diff; i++)
                    {
                        VM.Setting.Sounds.Add(new Core.Models.WarningSoundSetting()
                        {
                            Id = VM.Setting.Sounds.Count
                        });
                    }
                }
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
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                (Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow).ShowError(ex.Message);
            }
        }
    }
}
