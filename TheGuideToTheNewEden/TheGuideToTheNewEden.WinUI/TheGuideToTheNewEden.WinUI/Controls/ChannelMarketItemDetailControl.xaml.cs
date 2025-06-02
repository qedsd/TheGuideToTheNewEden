using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Controls.KBListControl;

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

        public static readonly DependencyProperty ItemDetailCommandProperty
           = DependencyProperty.Register(
               nameof(ItemDetailCommand),
               typeof(ICommand),
               typeof(ChannelMarketItemDetailControl),
               new PropertyMetadata(default(ICommand)));

        public ICommand ItemDetailCommand
        {
            get => (ICommand)GetValue(ItemDetailCommandProperty);
            set => SetValue(ItemDetailCommandProperty, value);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ItemDetailCommand?.Execute(Result.Item);
            ItemDetailClicked?.Invoke(this, Result.Item);
        }

        private EventHandler<InvTypeBase> ItemDetailClicked;
        public event EventHandler<InvTypeBase> OnItemDetailClicked
        {
            add
            {
                ItemDetailClicked += value;
            }
            remove
            {
                ItemDetailClicked -= value;
            }
        }
    }
}
