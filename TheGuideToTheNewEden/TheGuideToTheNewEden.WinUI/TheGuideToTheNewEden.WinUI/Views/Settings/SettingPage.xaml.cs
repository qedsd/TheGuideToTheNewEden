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
    public sealed partial class SettingPage : Page, IPage
    {
        private Storyboard _showStoryboard;
        private Storyboard _hideStoryboard;
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
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_General"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.AppsSettings, typeof(GeneralSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_GameLog"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.CommentEdit, typeof(GameLogSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Structures"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.BuildingGovernmentSearch, typeof(StructuresSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Market"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.ArrowTrendingSettings, typeof(MarketSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_ZKB"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.ScanObject, typeof(ZKBSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_ESIScope"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.NavigationPerson, typeof(ESIScopeSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_Test"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.Bug, typeof(TestSettingPage)),
                new SettingItem(Helpers.ResourcesHelper.GetString("SettingPage_KeyboardList"), Helpers.ResourcesHelper.GetString(""), FluentIcons.Common.Icon.Keyboard, typeof(KeyboardListPage)),

            };
            _showStoryboard = CreateStoryboard(0, 1, 0.2);
            _hideStoryboard = CreateStoryboard(1, 0, 0.2);
            _hideStoryboard.Completed += HideStoryboard_Completed;
            _showStoryboard.Completed += ShowStoryboard_Completed;
        }

        private void HideStoryboard_Completed(object sender, object e)
        {
            SettingFrame.Visibility = Visibility.Visible;
            SettingList.Visibility = Visibility.Collapsed;
        }

        private void ShowStoryboard_Completed(object sender, object e)
        {
            
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
            //_showStoryboard.Begin();
        }

        private Storyboard CreateStoryboard(double from, double to, double seconds)
        {
            Storyboard storyboard = new Storyboard();

            // 创建DoubleAnimation用于改变宽度
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(seconds),
                AutoReverse = false,
            };

            // 设置动画目标
            Storyboard.SetTarget(animation, SettingList);
            Storyboard.SetTargetProperty(animation, "Opacity");

            // 将动画添加到Storyboard
            storyboard.Children.Add(animation);
            return storyboard;
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            Selected = ((Button)sender).DataContext as SettingItem;
            SecondSettingTitle = Selected?.Title;
            if (Selected != null)
            {
                SecondSetting.Visibility = Visibility.Visible;
                SettingFrame.Visibility = Visibility.Visible;
                SettingList.Visibility = Visibility.Collapsed;
                SettingFrame.Content = Selected.GetInstance();
                SettingList.SelectedItem = null;
            }
        }
    }
}
