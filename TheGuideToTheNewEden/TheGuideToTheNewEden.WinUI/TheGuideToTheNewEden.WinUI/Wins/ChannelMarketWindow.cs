using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ChannelMarketWindow : BaseWindow
    {
        private Dictionary<int, string> _regionNames = new Dictionary<int, string>();
        private Views.ChannelMarketWinPage _page;
        private string _title;
        public ChannelMarketWindow()
        {
            HideAppDisplayName();
            _title = Helpers.ResourcesHelper.GetString("ShellPage_ChannelMarket");
            this.Title = _title;
            HideNavButton();
            MainContentExtendsToTitleBar();
            SetHeadText(_title);
            _page = new Views.ChannelMarketWinPage();
            _page.SetWindow(this);
            MainContent = _page;
            this.SetWindowSize(600, 600);
            this.SetIsAlwaysOnTop(true);
        }
        public void UpdateContent(IEnumerable<MarketChatContent> items, int regionID)
        {
            string regionName;
            if(!_regionNames.TryGetValue(regionID, out regionName))
            {
                var region = Core.Services.DB.MapRegionService.Query(regionID);
                if(region != null)
                {
                    _regionNames.Add(regionID, region.RegionName);
                    regionName = region.RegionName;
                }
            }
            SetHeadText($"{_title} - {regionName}");
            _page.UpdateContent(items, regionID);
        }
    }
}
