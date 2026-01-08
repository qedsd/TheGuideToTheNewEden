using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Views.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class SettingPage : Page, IPage, INotifyPropertyChanged
    {
        public List<SettingItem> SettingItems { get; set; }
        private SettingItem _selected;
        public SettingItem Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }
        private string _secondSettingTitle;
        public string SecondSettingTitle
        {
            get => _secondSettingTitle;
            set
            {
                _secondSettingTitle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SecondSettingTitle)));
            }
        }
        public SettingPage()
        {
            InitializeComponent();
            SettingItems = new List<SettingItem>()
            {
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_General"), Helpers.ResourcesHelper.GetString("SettingPage_General_Desc"), FluentIcons.Common.Icon.AppsSettings, typeof(GeneralSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_GameLog"), Helpers.ResourcesHelper.GetString("SettingPage_GameLog_Desc"), FluentIcons.Common.Icon.CommentEdit, typeof(GameLogSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Structures"), Helpers.ResourcesHelper.GetString("SettingPage_Structures_Desc"), FluentIcons.Common.Icon.BuildingGovernmentSearch, typeof(StructuresSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Market"), Helpers.ResourcesHelper.GetString("SettingPage_Market_Desc"), FluentIcons.Common.Icon.ArrowTrendingSettings, typeof(MarketSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_ZKB"), Helpers.ResourcesHelper.GetString("SettingPage_ZKB_Desc"), FluentIcons.Common.Icon.ScanObject, typeof(ZKBSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_ESIScope"), Helpers.ResourcesHelper.GetString("SettingPage_ESIScope_Desc"), FluentIcons.Common.Icon.NavigationPerson, typeof(ESIScopeSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Test"), Helpers.ResourcesHelper.GetString("SettingPage_Test_Desc"), FluentIcons.Common.Icon.Bug, typeof(TestSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_KeyboardList"), Helpers.ResourcesHelper.GetString("SettingPage_KeyboardList_Desc"), FluentIcons.Common.Icon.Keyboard, typeof(KeyboardListPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Update"), Helpers.ResourcesHelper.GetString("SettingPage_Update_Desc"), FluentIcons.Common.Icon.ArrowSync, typeof(UpdatePage)),
            };
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is object[] ps && ps.Length > 0)
            {
                switch (ps[0].ToString())
                {
                    case "Update":
                        {
                            SetSelecteItem(SettingItems.FirstOrDefault(p => p.PageType == typeof(UpdatePage)));
                        }break;
                    case "About":
                        {
                            SetSelecteItem(SettingItems.FirstOrDefault(p => p.PageType == typeof(AboutPage)));
                        }
                        break;
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public void Init()
        {

        }

        public void Close()
        {

        }

        private void FirstSetting_Clicked(object sender, RoutedEventArgs e)
        {
            SettingFrame.Visibility = Visibility.Collapsed;
            SettingList.Visibility = Visibility.Visible;
            SecondSetting.Visibility = Visibility.Collapsed;
            FirstSettinButton.Opacity = 1;
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            SetSelecteItem(((Button)sender).DataContext as SettingItem);
        }
        private void SetSelecteItem(SettingItem item)
        {
            Selected = item;
            SecondSettingTitle = Selected?.Title;
            if (Selected != null)
            {
                SettingFrame.Navigate(Selected.PageType);
                SecondSetting.Visibility = Visibility.Visible;
                SettingFrame.Visibility = Visibility.Visible;
                SettingList.Visibility = Visibility.Collapsed;
                FirstSettinButton.Opacity = 0.6;
            }
        }
    }
}
