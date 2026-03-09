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

        private float _maxMsgCount = 100;
        /// <summary>
        /// 最大显示信息数量
        /// </summary>
        public float MaxMsgCount
        {
            get => _maxMsgCount;
            set => SetProperty(ref _maxMsgCount, value);
        }

        private ObservableCollection<IdName> zkbFilter = new ObservableCollection<IdName>();
        /// <summary>
        /// ZKB攻击者在此列表内将不显示到星图
        /// </summary>
        public ObservableCollection<IdName> ZKBFilter
        {
            get => zkbFilter;
            set => SetProperty(ref zkbFilter, value);
        }
    }
}
