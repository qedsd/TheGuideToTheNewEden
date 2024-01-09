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
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBListControl : UserControl
    {
        public KBListControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty KBsProperty
           = DependencyProperty.Register(
               nameof(KBs),
               typeof(IEnumerable<KBItemInfo>),
               typeof(KBSystemInfoControl),
               new PropertyMetadata(null, new PropertyChangedCallback(KBsPropertyChanged)));

        public IEnumerable<KBItemInfo> KBs
        {
            get => (IEnumerable<KBItemInfo>)GetValue(KBsProperty);
            set
            {
                SetValue(KBsProperty, value);
                DataGrid.ItemsSource = value;
            }
        }
        private static void KBsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private void MenuFlyoutItem_Browser_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void SfDataGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }

        private void KBListCharacterControl_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as KBListCharacterControl).KBItemInfo = (sender as KBListCharacterControl).DataContext as KBItemInfo;
        }
    }
}
