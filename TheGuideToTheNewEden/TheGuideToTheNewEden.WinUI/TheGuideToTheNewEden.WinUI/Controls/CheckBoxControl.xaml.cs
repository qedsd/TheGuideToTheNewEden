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
    public sealed partial class CheckBoxControl : UserControl
    {
        public CheckBoxControl()
        {
            Loaded += CheckBoxControl_Loaded;
            this.InitializeComponent();
        }

        private void CheckBoxControl_Loaded(object sender, RoutedEventArgs e)
        {
            box.IsChecked = (bool?)IsChecked;
        }

        public static readonly DependencyProperty IsCheckedProperty
           = DependencyProperty.Register(
               nameof(IsChecked),
               typeof(object),
               typeof(CheckBoxControl),
               new PropertyMetadata(default, new PropertyChangedCallback(IsCheckedPropertyChanged)));

        public object IsChecked
        {
            get => (object)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }
        private static void IsCheckedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CheckBoxControl).box.IsChecked = (bool?)e.NewValue;
        }

        public static readonly DependencyProperty IsThreeStateProperty
          = DependencyProperty.Register(
              nameof(IsThreeState),
              typeof(bool),
              typeof(CheckBoxControl),
              new PropertyMetadata(true, new PropertyChangedCallback(IsThreeStatePropertyChanged)));

        public bool IsThreeState
        {
            get => (bool)GetValue(IsThreeStateProperty);
            set => SetValue(IsThreeStateProperty, value);
        }
        private static void IsThreeStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CheckBoxControl).box.IsChecked = (bool?)e.NewValue;
        }
    }
}
