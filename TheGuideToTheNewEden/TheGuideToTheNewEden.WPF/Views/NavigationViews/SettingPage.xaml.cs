using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TheGuideToTheNewEden.WPF.Models;
using TheGuideToTheNewEden.WPF.Views.Settings;

namespace TheGuideToTheNewEden.WPF.Views.NavigationViews
{
    public partial class SettingPage : Page, IPage, INotifyPropertyChanged
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
                new SettingItem("Navigation.SettingPage_General", "Navigation.SettingPage_General_Desc", FluentIcons.Common.Icon.AppsSettings, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_GameLog", "Navigation.SettingPage_GameLog_Desc", FluentIcons.Common.Icon.DocumentBulletList, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_Structures", "Navigation.SettingPage_Structures_Desc", FluentIcons.Common.Icon.HomeDatabase, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_Market", "Navigation.SettingPage_Market_Desc", FluentIcons.Common.Icon.Cart, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_Zkillboard", "Navigation.SettingPage_Zkillboard_Desc", FluentIcons.Common.Icon.Drag, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_ESIScope", "Navigation.SettingPage_ESIScope_Desc", FluentIcons.Common.Icon.SlideText, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_Test", "Navigation.SettingPage_Test_Desc", FluentIcons.Common.Icon.Bug, typeof(GeneralSettingPage)),
                new SettingItem("Navigation.SettingPage_KeyboardList", "Navigation.SettingPage_KeyboardList_Desc", FluentIcons.Common.Icon.Keyboard, typeof(GeneralSettingPage)),
            };
            _showStoryboard = CreateStoryboard(0, 1, 0.2);
            _hideStoryboard = CreateStoryboard(1, 0, 0.2);
            _hideStoryboard.Completed += HideStoryboard_Completed;
            _showStoryboard.Completed += ShowStoryboard_Completed;
        }

        private void HideStoryboard_Completed(object sender, EventArgs e)
        {
            SettingFrame.Visibility = Visibility.Visible;
            SettingList.Visibility = Visibility.Collapsed;
        }

        private void ShowStoryboard_Completed(object sender, EventArgs e)
        {
            //
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Init()
        {
            
        }

        public void Close()
        {
            
        }

        private void ListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Selected = ((ListBox)sender).SelectedItem as SettingItem;
            SecondSettingTitle = Selected?.Title;
            if (Selected != null)
            {
                SecondSetting.Visibility = Visibility.Visible;
                //_hideStoryboard.Begin();
                SettingFrame.Visibility = Visibility.Visible;
                SettingList.Visibility = Visibility.Collapsed;
                SettingFrame.Navigate(Selected.GetInstance());
                ((ListBox)sender).SelectedItem = null;
            }
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
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));

            // 将动画添加到Storyboard
            storyboard.Children.Add(animation);
            return storyboard;
        }
    }
}
