using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.Core.Models.Channel
{
    public class ChannelSetting : ObservableObject
    {
        public string CharacterName { get; set; }
        public List<string> Channels { get; set; } = new List<string>();
    }
}
