using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using TheGuideToTheNewEden.WinUI.Views.Character;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class NavigatePageControl : UserControl
    {
        public NavigatePageControl()
        {
            this.InitializeComponent();
            Loaded += NavigatePageControl_Loaded;
        }

        private void NavigatePageControl_Loaded(object sender, RoutedEventArgs e)
        {
            PageBox.Minimum = MinPage;
            PageBox.Maximum = MaxPage;
            CheckButton();
        }

        #region 依赖属性
        #region 页数
        public static readonly DependencyProperty PageProperty
            = DependencyProperty.Register(
                nameof(Page),
                typeof(int),
                typeof(NavigatePageControl),
                new PropertyMetadata(1, new PropertyChangedCallback(PagePropertyChanged)));

        public int Page
        {
            get => (int)GetValue(PageProperty);
            set => SetValue(PageProperty, value);
        }
        private static void PagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as NavigatePageControl).PageBox.Value = (int)e.NewValue;
            (d as NavigatePageControl).PageChanged?.Invoke((int)e.NewValue);
        }
        #endregion

        #region 最小值
        public static readonly DependencyProperty MinPageProperty
            = DependencyProperty.Register(
                nameof(MinPage),
                typeof(int),
                typeof(NavigatePageControl),
                new PropertyMetadata(1,new PropertyChangedCallback(MinPagePropertyChanged)));

        public int MinPage
        {
            get => (int)GetValue(MinPageProperty);
            set => SetValue(MinPageProperty, value);
        }
        private static void MinPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as NavigatePageControl).PageBox.Minimum = (int)e.NewValue;
            (d as NavigatePageControl).CheckButton();
        }
        #endregion

        #region 最大值
        public static readonly DependencyProperty MaxPageProperty
            = DependencyProperty.Register(
                nameof(MaxPage),
                typeof(int),
                typeof(NavigatePageControl),
                new PropertyMetadata(int.MaxValue,new PropertyChangedCallback(MaxPagePropertyChanged)));

        public int MaxPage
        {
            get => (int)GetValue(MaxPageProperty);
            set => SetValue(MaxPageProperty, value);
        }
        private static void MaxPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as NavigatePageControl).PageBox.Minimum = (int)e.NewValue;
            (d as NavigatePageControl).CheckButton();
        }
        #endregion

        #region 上一页
        public static readonly DependencyProperty PreviousCommandProperty
            = DependencyProperty.Register(
                nameof(PreviousCommand),
                typeof(ICommand),
                typeof(NavigatePageControl),
                new PropertyMetadata(default(ICommand)));
        public ICommand PreviousCommand
        {
            get => (ICommand)GetValue(PreviousCommandProperty);
            set => SetValue(PreviousCommandProperty, value);
        }
        #endregion

        #region 下一页
        public static readonly DependencyProperty NextCommandProperty
            = DependencyProperty.Register(
                nameof(NextCommand),
                typeof(ICommand),
                typeof(NavigatePageControl),
                new PropertyMetadata(default(ICommand)));
        public ICommand NextCommand
        {
            get => (ICommand)GetValue(NextCommandProperty);
            set => SetValue(NextCommandProperty, value);
        }
        #endregion

        #region 跳至
        public static readonly DependencyProperty GoCommandProperty
            = DependencyProperty.Register(
                nameof(GoCommand),
                typeof(ICommand),
                typeof(NavigatePageControl),
                new PropertyMetadata(default(ICommand)));
        public ICommand GoCommand
        {
            get => (ICommand)GetValue(GoCommandProperty);
            set => SetValue(GoCommandProperty, value);
        }
        #endregion

        #endregion

        #region 按钮事件
        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            if(Page >= MinPage && Page <= int.MaxValue)
            {
                GoCommand?.Execute(Page);
                CheckButton();
            }
        }

        private void PreButton_Click(object sender, RoutedEventArgs e)
        {
            if(Page - 1 >= MinPage)
            {
                Page--;
                PreviousCommand?.Execute(Page);
                CheckButton();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (Page + 1 <= MaxPage)
            {
                Page++;
                NextCommand?.Execute(Page);
                CheckButton();
            }
        }

        private void CheckButton()
        {
            PreButton.IsEnabled = Page > MinPage;
            NextButton.IsEnabled = Page < MaxPage;
        }
        #endregion

        #region 页数更改事件
        public delegate void PageChangedEventHandel(int page);
        private PageChangedEventHandel PageChanged;

        public static readonly DependencyProperty PageChangedCommandProperty
           = DependencyProperty.Register(
               nameof(PageChangedCommand),
               typeof(int),
               typeof(NavigatePageControl),
               new PropertyMetadata(default(ICommand)));

        public ICommand PageChangedCommand
        {
            get => (ICommand)GetValue(PageChangedCommandProperty);
            set => SetValue(PageChangedCommandProperty, value);
        }

        public event PageChangedEventHandel OnPageChanged
        {
            add
            {
                PageChanged += value;
            }
            remove
            {
                PageChanged -= value;
            }
        }
        #endregion

        private void PageBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            Page = (int)sender.Value;
        }
    }
}
