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
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.WinUI.Extensions;
using WinUIEx;
using TheGuideToTheNewEden.WinUI.Interfaces;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ChannelMarketWinPage : Page
    {
        public ChannelMarketWinPage()
        {
            this.InitializeComponent();
            SumGrid.Translation += new System.Numerics.Vector3(0, 0, 32);
        }
        public void SetWindow(IWindow window)
        {
            VM.SetWindow(window);
        }
        public void UpdateContent(IEnumerable<MarketChatContent> marketChatContents, int regionId)
        {
            VM.UpdateContent(marketChatContents, regionId);
        }

        private void ChannelMarketItemDetailControl_OnItemDetailClicked(object sender, Core.DBModels.InvTypeBase e)
        {
            VM.DetailCommand.Execute(e);
        }
    }
}
