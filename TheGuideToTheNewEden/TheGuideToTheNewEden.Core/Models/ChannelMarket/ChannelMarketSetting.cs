using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.Core.Models.ChannelMarket
{
    public class ChannelMarketSetting : ObservableObject
    {
        public string CharacterName { get; set; }
        public List<string> Channels { get; set; }

        private int marketRegionID = 10000002;
        public int MarketRegionID
        {
            get => marketRegionID; set => SetProperty(ref marketRegionID, value);
        }

        private string keyWord = string.Empty;
        public string KeyWord
        {
            get => keyWord; set => SetProperty(ref keyWord, value);
        }

        private string itemsSeparator = "*";
        public string ItemsSeparator
        {
            get => itemsSeparator; set => SetProperty(ref itemsSeparator, value);
        }
    }
}
