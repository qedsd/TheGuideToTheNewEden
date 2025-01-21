using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class ImageControl : UserControl
    {
        public ImageControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty SourceProperty
           = DependencyProperty.Register(
               nameof(Source),
               typeof(string),
               typeof(ImageControl),
               new PropertyMetadata(null, new PropertyChangedCallback(SourcePropertyPropertyChanged)));

        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set
            {
                SetValue(SourceProperty, value);
            }
        }

        private static void SourcePropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ImageControl;
            if(e.NewValue != null)
            {
                var value = e.NewValue as string;
                control.SourceImage.Source = new BitmapImage(new Uri(value));
            }
            else
            {
                control.SourceImage.Source = null;
            }
        }

        public static readonly new DependencyProperty CornerRadiusProperty
           = DependencyProperty.Register(
               nameof(CornerRadius),
               typeof(CornerRadius),
               typeof(ImageControl),
               new PropertyMetadata(null, new PropertyChangedCallback(CornerRadiusPropertyPropertyChanged)));

        public new CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set
            {
                SetValue(CornerRadiusProperty, value);
            }
        }

        private static void CornerRadiusPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ImageControl;
            var value = (CornerRadius)e.NewValue;
            control.Border.CornerRadius = value;
        }
    }
}
