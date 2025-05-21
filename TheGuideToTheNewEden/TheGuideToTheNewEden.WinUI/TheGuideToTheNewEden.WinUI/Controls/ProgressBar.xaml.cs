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
using Microsoft.UI;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class ProgressBar : UserControl
    {
        public ProgressBar()
        {
            this.InitializeComponent();
        }

        #region value
        public static readonly DependencyProperty ValueProperty
            = DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ProgressBar),
                new PropertyMetadata(2, new PropertyChangedCallback(ValuePropertyChanged)));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        private static void ValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue.GetType() == typeof(int))
            {
                (d as ProgressBar).MainProgressBar.Value = (int)e.NewValue;
            }
            else
            {
                (d as ProgressBar).MainProgressBar.Value = (double)e.NewValue;
            }
        }
        #endregion

        #region min value
        public static readonly DependencyProperty MinimumProperty
            = DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(ProgressBar),
                new PropertyMetadata(0, new PropertyChangedCallback(MinimumPropertyChanged)));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }
        private static void MinimumPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue.GetType() == typeof(int))
            {
                (d as ProgressBar).MainProgressBar.Minimum = (int)e.NewValue;
            }
            else
            {
                (d as ProgressBar).MainProgressBar.Minimum = (double)e.NewValue;
            }
        }
        #endregion

        #region max value
        public static readonly DependencyProperty MaximumProperty
            = DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(ProgressBar),
                new PropertyMetadata(0, new PropertyChangedCallback(MaximumPropertyChanged)));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }
        private static void MaximumPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue.GetType() == typeof(int))
            {
                (d as ProgressBar).MainProgressBar.Maximum = (int)e.NewValue;
            }
            else
            {
                (d as ProgressBar).MainProgressBar.Maximum = (double)e.NewValue;
            }
        }
        #endregion

        #region 主颜色
        public static readonly DependencyProperty ValueColorProperty
            = DependencyProperty.Register(
                nameof(ValueColor),
                typeof(Brush),
                typeof(ProgressBar),
                new PropertyMetadata(Colors.Blue, new PropertyChangedCallback(ValueColorPropertyChanged)));

        public Brush ValueColor
        {
            get => (Brush)GetValue(ValueColorProperty);
            set => SetValue(ValueColorProperty, value);
        }
        private static void ValueColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as ProgressBar).MainProgressBar.Foreground = (Brush)e.NewValue;
        }
        #endregion

        #region 背景颜色
        public static readonly DependencyProperty BgColorProperty
           = DependencyProperty.Register(
               nameof(BgColor),
               typeof(Brush),
               typeof(ProgressBar),
               new PropertyMetadata(Colors.LightGray, new PropertyChangedCallback(BgColorPropertyChanged)));

        public Brush BgColor
        {
            get => (Brush)GetValue(BgColorProperty);
            set => SetValue(BgColorProperty, value);
        }
        private static void BgColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as ProgressBar).MainProgressBar.Background = (Brush)e.NewValue;
            (d as ProgressBar).MainGrid.Background = (Brush)e.NewValue;
        }
        #endregion

        #region 高度
        public static readonly DependencyProperty BarHeightProperty
          = DependencyProperty.Register(
              nameof(BarHeight),
              typeof(double),
              typeof(ProgressBar),
              new PropertyMetadata(2, new PropertyChangedCallback(BarHeightPropertyChanged)));

        public double BarHeight
        {
            get => (double)GetValue(BarHeightProperty);
            set => SetValue(BarHeightProperty, value);
        }
        private static void BarHeightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as ProgressBar).MainProgressBar.MinHeight = (double)e.NewValue;
            (d as ProgressBar).MainGrid.Height = (double)e.NewValue;
        }
        #endregion
    }
}
