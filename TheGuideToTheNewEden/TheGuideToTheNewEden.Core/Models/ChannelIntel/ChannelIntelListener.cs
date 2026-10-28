using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.ChannelIntel
{
    public class ChannelIntelListener : ObservableObject
    {
        public string Name {  get; set; }

        /// <summary>ESI 角色 ID；解析不到时为 0。</summary>
        public long CharacterId { get; set; }

        /// <summary>ESI 头像 URL；拿不到时为空，界面不显示头像。</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>列表头像用的首字符（角色名通常为英文，取第一个字符即可）。</summary>
        public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpperInvariant();

        private bool _running;
        public bool Running
        {
            get => _running;
            set => SetProperty(ref _running, value);
        }

        public ChannelIntelListener(string name)
        {
            Name = name;
        }
    }
}
