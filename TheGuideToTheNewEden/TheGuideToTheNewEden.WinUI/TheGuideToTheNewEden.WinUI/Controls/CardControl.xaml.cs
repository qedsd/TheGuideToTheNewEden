using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;


namespace TheGuideToTheNewEden.WinUI.Controls
{
    [Microsoft.UI.Xaml.Markup.ContentProperty(Name = nameof(SettingActionableElement))]
    public sealed partial class CardControl : UserControl
    {
        public FrameworkElement SettingActionableElement { get; set; }

        public CardControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty HeaderProperty
            = DependencyProperty.Register(
                nameof(Header),
                typeof(FrameworkElement),
                typeof(CardControl),
                new PropertyMetadata(null));

        public FrameworkElement Header
        {
            get => (FrameworkElement)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static new readonly DependencyProperty ContentProperty
            = DependencyProperty.Register(
                nameof(Content),
                typeof(FrameworkElement),
                typeof(CardControl),
                new PropertyMetadata(null));

        public new FrameworkElement Content
        {
            get => (FrameworkElement)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty FonterProperty
           = DependencyProperty.Register(
               nameof(Fonter),
               typeof(FrameworkElement),
               typeof(CardControl),
               new PropertyMetadata(null, new PropertyChangedCallback(FonterPropertyPropertyChanged)));

        public FrameworkElement Fonter
        {
            get => (FrameworkElement)GetValue(FonterProperty);
            set => SetValue(FonterProperty, value);
        }

        private static void FonterPropertyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as CardControl;
            if(e.NewValue != null)
            {
                control.ContentPresenter.BorderThickness = new Thickness(1, 0, 1, 0);
                control.ContentPresenter.CornerRadius = new CornerRadius(0);
                control.FonterLine.Visibility = Visibility.Visible;
                control.FonterPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                control.ContentPresenter.CornerRadius = new CornerRadius(0,0, control.CornerRadius.BottomRight, control.CornerRadius.BottomLeft);
                control.FonterLine.Visibility = Visibility.Collapsed;
                control.FonterPresenter.Visibility = Visibility.Collapsed;
            }
        }
    }
}
