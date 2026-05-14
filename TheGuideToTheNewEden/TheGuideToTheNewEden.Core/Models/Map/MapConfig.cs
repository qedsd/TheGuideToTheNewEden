using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class MapConfig
    {
        public MapIntelConfig Intel { get; set; } = new MapIntelConfig();
    }
    public class MapIntelConfig : ObservableObject
    {
        private bool _zkb;
        public bool ZKB
        {
            get => _zkb;
            set => SetProperty(ref _zkb, value);
        }

        private bool _zkbAutoClear;
        public bool ZKBAutoClear
        {
            get => _zkbAutoClear;
            set => SetProperty(ref _zkbAutoClear, value);
        }

        private float _zkbDuration = 1200;
        /// <summary>
        /// 单位秒
        /// </summary>
        public float ZKBDuration
        {
            get => _zkbDuration;
            set => SetProperty(ref _zkbDuration, value);
        }

        private float _zkbMaxAttackerCount = 10;
        /// <summary>
        /// 最大显示击杀者数量
        /// </summary>
        public float ZKBMaxAttackerCount
        {
            get => _zkbMaxAttackerCount;
            set => SetProperty(ref _zkbMaxAttackerCount, value);
        }

        private float _maxMsgCount = 1000;
        /// <summary>
        /// 最大显示信息数量
        /// </summary>
        public float MaxMsgCount
        {
            get => _maxMsgCount;
            set => SetProperty(ref _maxMsgCount, value);
        }

        private ObservableCollection<IdName> _exclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 排除项
        /// </summary>
        public ObservableCollection<IdName> Exclusions
        {
            get => _exclusions;
            set => SetProperty(ref _exclusions, value);
        }

        private ObservableCollection<IdName> _inclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 包含项
        /// </summary>
        public ObservableCollection<IdName> Inclusions
        {
            get => _inclusions;
            set => SetProperty(ref _inclusions, value);
        }

        private HashSet<string> _channels = new HashSet<string>();

        public HashSet<string> Channels
        {
            get => _channels;
            set => SetProperty(ref _channels, value);
        }

        private int _clearChannelMode;
        /// <summary>
        /// 频道预警触发清除时如何响应
        /// 0：清除频道+ZKB
        /// 1：只清除频道
        /// </summary>
        public int ClearChannelMode
        {
            get => _clearChannelMode;
            set => SetProperty(ref _clearChannelMode, value);
        }

        private float _channelDuration = 1200;
        /// <summary>
        /// 单位秒
        /// </summary>
        public float ChannelDuration
        {
            get => _channelDuration;
            set => SetProperty(ref _channelDuration, value);
        }
    }
}
