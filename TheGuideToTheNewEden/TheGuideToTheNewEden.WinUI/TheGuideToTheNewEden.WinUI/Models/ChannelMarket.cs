using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESI.NET.Models.Industry;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.Models
{
    /// <summary>
    /// 一个角色频道市场实例
    /// </summary>
    public class ChannelMarket
    {
        public ChannelMarketSetting Setting { get;private set; }
        private List<ChannelMarketObserver> _observers;
        public ChannelMarket(ChannelMarketSetting setting)
        {
            Setting = setting;
        }
        public ChannelMarket(string characterName)
        {
            Setting = ChannelMarketSettingService.GetValue(characterName);
            if (Setting == null)
            {
                Setting = new ChannelMarketSetting()
                {
                    CharacterName = characterName
                };
            }
        }
        public void SetSelectedChannels(List<string> paths)
        {
            Setting.Channels = paths;
        }
        public void Start()
        {
            foreach (var path in Setting.Channels)
            {
                ChannelMarketObserver observer = new ChannelMarketObserver(path, Setting.CharacterName, Setting.KeyWord, Setting.ItemsSeparator);
                if (Core.Services.ObservableFileService.Add(observer))
                {
                    observer.OnContentUpdate += Observer_OnContentUpdate;
                    _observers.Add(observer);
                }
            }
            Save();
        }

        private void Observer_OnContentUpdate(ChannelMarketObserver sender, IEnumerable<MarketChatContent> news)
        {
            if (news != null)
            {
                ChannelMarketService.Current.Query(news.Where(p=>p.Important), Setting.MarketRegionID);
            }
        }

        public void Stop()
        {
            Core.Services.ObservableFileService.Remove(_observers);
        }
        public void Save()
        {
            ChannelMarketSettingService.SetValue(Setting);
        }
    }
}
