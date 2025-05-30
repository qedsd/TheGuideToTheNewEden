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
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class ChannelMarketItemDetailControl : UserControl
    {
        public ChannelMarketItemDetailControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ResultProperty
            = DependencyProperty.Register(
                nameof(Result),
                typeof(ChannelMarketResult),
                typeof(ChannelMarketItemDetailControl),
                new PropertyMetadata(null));
        public ChannelMarketResult Result
        {
            get => (ChannelMarketResult)GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }
    }
}
