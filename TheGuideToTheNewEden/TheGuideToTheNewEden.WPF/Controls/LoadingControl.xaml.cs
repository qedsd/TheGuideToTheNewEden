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
    public partial class LoadingControl : UserControl
    {
        public LoadingControl()
        {
            InitializeComponent();
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
            if(!string.IsNullOrEmpty(control.LoadingTip.Text))
            {
                control.LoadingTip.Visibility = Visibility.Visible;
            }
            else
            {
                control.LoadingTip.Visibility = Visibility.Collapsed;
            }
        }
    }
}
