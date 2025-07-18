using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TheGuideToTheNewEden.WPF.Controls
{
    public partial class SettingControl : UserControl
    {
        public SettingControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty IconProperty
          = DependencyProperty.Register(
              nameof(Icon),
              typeof(FrameworkElement),
              typeof(SettingControl),
              new PropertyMetadata(default, new PropertyChangedCallback(IconPropertyChanged)));

        public FrameworkElement Icon
        {
            get => (FrameworkElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        private static void IconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SettingControl;
        }

        public static readonly DependencyProperty SettingContentProperty
          = DependencyProperty.Register(
              nameof(SettingContent),
              typeof(FrameworkElement),
              typeof(SettingControl),
              new PropertyMetadata(default, new PropertyChangedCallback(SettingContentPropertyChanged)));

        public FrameworkElement SettingContent
        {
            get => (FrameworkElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        private static void SettingContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SettingControl;
        }

        public static readonly DependencyProperty SettingTitleProperty
          = DependencyProperty.Register(
              nameof(SettingTitle),
              typeof(string),
              typeof(SettingControl),
              new PropertyMetadata(default));

        public string SettingTitle
        {
            get => (string)GetValue(SettingTitleProperty);
            set => SetValue(SettingTitleProperty, value);
        }

        public static readonly DependencyProperty SettingDescriptionProperty
          = DependencyProperty.Register(
              nameof(SettingDescription),
              typeof(string),
              typeof(SettingControl),
              new PropertyMetadata(default));

        public string SettingDescription
        {
            get => (string)GetValue(SettingDescriptionProperty);
            set => SetValue(SettingDescriptionProperty, value);
        }
    }
}
