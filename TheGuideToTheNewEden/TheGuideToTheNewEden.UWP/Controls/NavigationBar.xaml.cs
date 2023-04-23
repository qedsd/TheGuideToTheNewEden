using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TheGuideToTheNewEden.UWP.Controls
{
    public sealed partial class NavigationBar : UserControl
    {
        public NavigationBar()
        {
            this.InitializeComponent();
        }

        private void PageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter)
            {
                Page = int.Parse(PageTextBox.Text);
            }
        }

        public static readonly DependencyProperty PageProperty = DependencyProperty.Register
            (
                "Page",
                typeof(int),
                typeof(NavigationBar),
                new PropertyMetadata(1, new PropertyChangedCallback(SetPage))
            );

        private static void SetPage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var nv = (NavigationBar)d;
            if((int)e.NewValue > 1)
            {
                //nv.PrevButton.Visibility = Visibility.Visible;
                //nv.StartButton.Visibility = Visibility.Visible;
                nv.PrevButton.IsEnabled = true;
                nv.StartButton.IsEnabled = true;
            }
            else
            {
                //nv.PrevButton.Visibility = Visibility.Collapsed;
                //nv.StartButton.Visibility = Visibility.Collapsed;
                nv.PrevButton.IsEnabled = false;
                nv.StartButton.IsEnabled = false;
            }
            if(nv.MaxPage > 0)
            {
                if(nv.MaxPage < (int)e.NewValue + 1)
                {
                    //nv.NextButton.Visibility = Visibility.Collapsed;
                    //nv.EndButton.Visibility = Visibility.Collapsed;
                    nv.NextButton.IsEnabled = false;
                    nv.EndButton.IsEnabled = false;
                }
                else
                {
                    //nv.NextButton.Visibility = Visibility.Visible;
                    //nv.EndButton.Visibility = Visibility.Visible;
                    nv.NextButton.IsEnabled = true;
                    nv.EndButton.IsEnabled = true;
                }
            }
            else
            {
                //nv.NextButton.Visibility = Visibility.Visible;
                //nv.EndButton.Visibility = Visibility.Collapsed;
                nv.NextButton.IsEnabled = true;
                nv.EndButton.IsEnabled = false;
            }
            nv.OnPageChanged?.Invoke((int)e.NewValue);
        }
        public int Page
        {
            get { return (int)GetValue(PageProperty); }

            set { SetValue(PageProperty, value); }
        }

        public static readonly DependencyProperty MaxPageDep = DependencyProperty.Register
            (
                "MaxPage",
                typeof(int),
                typeof(NavigationBar),
                new PropertyMetadata(0, new PropertyChangedCallback(SetMaxPage))
            );

        private static void SetMaxPage(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var nv = (NavigationBar)d;
            if (nv.Page == (int)e.NewValue)
            {
                //nv.NextButton.Visibility = Visibility.Collapsed;
                //nv.EndButton.Visibility = Visibility.Collapsed;
                nv.NextButton.IsEnabled = false;
                nv.EndButton.IsEnabled = false;
            }
            else
            {
                //nv.NextButton.Visibility = Visibility.Visible;
                //nv.EndButton.Visibility = Visibility.Visible;
                nv.NextButton.IsEnabled = true;
                nv.EndButton.IsEnabled = true;
            }
        }
        public int MaxPage
        {
            get { return (int)GetValue(MaxPageDep); }

            set { SetValue(MaxPageDep, value); }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Page = 1;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            Page -= 1;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Page += 1;
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            Page = MaxPage;
        }
        public static readonly DependencyProperty PageChangedD = DependencyProperty.Register
            (
                "OnPageChanged",
                typeof(int),
                typeof(PageChangedDelegate),
                new PropertyMetadata(0, new PropertyChangedCallback(SetOnPageChanged))
            );
        private static void SetOnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NavigationBar)d).OnPageChanged = (PageChangedDelegate)e.NewValue;
        }
        public delegate void PageChangedDelegate(int page);
        public PageChangedDelegate OnPageChanged { get; set; }
    }
}
