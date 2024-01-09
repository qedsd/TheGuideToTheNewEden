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
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WinUI.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBSystemInfoControl : UserControl
    {
        public KBSystemInfoControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty SystemProperty
            = DependencyProperty.Register(
                nameof(System),
                typeof(MapSolarSystem),
                typeof(KBSystemInfoControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SystemPropertyChanged)));

        public MapSolarSystem System
        {
            get => (MapSolarSystem)GetValue(SystemProperty);
            set
            {
                SetValue(SystemProperty, value);
                if(value != null)
                {
                    TextBlock_SystemSecurity.Text = SystemSecurityFormatConverter.Convert(value.Security).ToString();
                    TextBlock_SystemSecurity.Foreground = SystemSecurityForegroundConverter.Convert(value.Security);
                    TextBlock_SystemName.Text = value.SolarSystemName;
                }
            }
        }
        private static void SystemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            
        }

        public static readonly DependencyProperty RegionProperty
            = DependencyProperty.Register(
                nameof(Region),
                typeof(MapRegion),
                typeof(KBSystemInfoControl),
                new PropertyMetadata(null, new PropertyChangedCallback(RegionPropertyChanged)));

        public MapRegion Region
        {
            get => (MapRegion)GetValue(RegionProperty);
            set
            {
                SetValue(RegionProperty, value);
                TextBlock_RegionName.Text = value?.RegionName;
            }
        }
        private static void RegionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private void Button_System_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Region_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
