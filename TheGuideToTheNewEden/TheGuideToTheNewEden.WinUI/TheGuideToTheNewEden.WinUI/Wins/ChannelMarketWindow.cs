using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ChannelMarketWindow : BaseWindow
    {
        private Views.ChannelMarketWinPage _page;
        public ChannelMarketWindow()
        {
            HideAppDisplayName();
            this.Title = Helpers.ResourcesHelper.GetString("ShellPage_ChannelMarket");
            HideNavButton();
            MainContentExtendsToTitleBar();
            SetHeadText(Helpers.ResourcesHelper.GetString("ShellPage_ChannelMarket"));
            _page = new Views.ChannelMarketWinPage();
            _page.SetWindow(this);
            MainContent = _page;
            this.SetWindowSize(600, 600);
        }
        public void UpdateContent(IEnumerable<MarketChatContent> items, int regionID)
        {
            _page.UpdateContent(items, regionID);
        }
    }
}
