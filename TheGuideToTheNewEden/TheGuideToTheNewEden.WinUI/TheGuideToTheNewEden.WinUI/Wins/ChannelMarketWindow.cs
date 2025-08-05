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
    internal class ChannelMarketWindow : ToolWindow
    {
        private Dictionary<int, string> _regionNames = new Dictionary<int, string>();
        private Views.ChannelMarketWinPage _page;
        private string _title;
        public ChannelMarketWindow()
        {
            _page = new Views.ChannelMarketWinPage();
            _title = Helpers.ResourcesHelper.GetString("ShellPage_ChannelMarket");
            InitWindow(_page, WindowTitleStyle.OnlyClose, true, true, true, true);

            SetDisplayTitle(_title);
            SetWindowTitle(Title);
            SetCloseToHide();

            _page.SetWindow(this);
            this.SetSize(600, 600);
            this.SetAlwaysOnTop();
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
            SetDisplayTitle($"{_title} - {regionName}");
            _page.UpdateContent(items, regionID);
        }
    }
}
