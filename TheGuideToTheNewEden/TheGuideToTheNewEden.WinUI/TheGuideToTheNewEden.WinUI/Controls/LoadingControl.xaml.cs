using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class LoadingControl : UserControl
    {
        public LoadingControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty IsLoadingProperty
           = DependencyProperty.Register(
               nameof(IsLoading),
               typeof(bool),
               typeof(LoadingControl),
               new PropertyMetadata(true, new PropertyChangedCallback(IsLoadingPropertyChanged)));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        private static void IsLoadingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as LoadingControl;
            if ((bool)e.NewValue)
            {
                control.storyboard1.Begin();
                control.storyboard2.Begin();
                control.storyboard3.Begin();
            }
            else
            {
                control.storyboard1.Stop();
                control.storyboard2.Stop();
                control.storyboard3.Stop();
            }
        }

        public static readonly DependencyProperty LoadingContentProperty
           = DependencyProperty.Register(
               nameof(LoadingContent),
               typeof(string),
               typeof(LoadingControl),
               new PropertyMetadata(null, new PropertyChangedCallback(LoadingContentPropertyChanged)));

        public string LoadingContent
        {
            get => (string)GetValue(LoadingContentProperty);
            set => SetValue(LoadingContentProperty, value);
        }

        private static void LoadingContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as LoadingControl;
            control.LoadingTip.Text = (string)e.NewValue;
            if (!string.IsNullOrEmpty(control.LoadingTip.Text))
            {
                control.LoadingTip.Visibility = Visibility.Visible;
            }
            else
            {
                control.LoadingTip.Visibility = Visibility.Collapsed;
            }
        }

        public static readonly DependencyProperty CancelCallbackProperty
           = DependencyProperty.Register(
               nameof(CancelCallback),
               typeof(CancelWaitingCallbackDelegate),
               typeof(LoadingControl),
               new PropertyMetadata(null, new PropertyChangedCallback(CancelCallbackPropertyChanged)));

        public CancelWaitingCallbackDelegate CancelCallback
        {
            get => (CancelWaitingCallbackDelegate)GetValue(CancelCallbackProperty);
            set => SetValue(CancelCallbackProperty, value);
        }
        private static void CancelCallbackPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as LoadingControl;
            var callback = e.NewValue as CancelWaitingCallbackDelegate;
            if (callback == null)
            {
                control.CancelButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.CancelButton.Visibility = Visibility.Visible;
            }
        }

        public delegate void CancelWaitingCallbackDelegate();

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (CancelCallback != null)
            {
                CancelCallback.Invoke();
            }
        }
    }
}
