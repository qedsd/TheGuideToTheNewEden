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

        /// <summary>
        /// 星图画布显示项（WPF 版）。
        /// </summary>
        public MapCanvasConfig Canvas { get; set; } = new MapCanvasConfig();
    }

    /// <summary>
    /// 星图画布的显示开关（逐着色类型独立保存；JSON 缺字段时用默认值）。
    /// </summary>
    public class MapCanvasConfig : ObservableObject
    {
        private bool _showHeatKills = true;
        /// <summary>热力色块：击杀模式是否显示。</summary>
        public bool ShowHeatKills
        {
            get => _showHeatKills;
            set => SetProperty(ref _showHeatKills, value);
        }

        private bool _showHeatJumps = true;
        /// <summary>热力色块：通行模式是否显示。</summary>
        public bool ShowHeatJumps
        {
            get => _showHeatJumps;
            set => SetProperty(ref _showHeatJumps, value);
        }

        private bool _showHeatPlanetResource = true;
        /// <summary>热力色块：行星资源模式是否显示。</summary>
        public bool ShowHeatPlanetResource
        {
            get => _showHeatPlanetResource;
            set => SetProperty(ref _showHeatPlanetResource, value);
        }

        private int _heatGridSize = 56;
        /// <summary>
        /// 热力网格格数（世界长边切成多少格；格数越少色块越大）。使用方由画布 Clamp 到 Min/MaxHeatGridCells。
        /// </summary>
        public int HeatGridSize
        {
            get => _heatGridSize;
            set => SetProperty(ref _heatGridSize, value);
        }

        private double _heatGridOffsetX;
        /// <summary>热力网格原点的归一化偏移（相对数据包围盒左上角，正 = 右移；1.0 = 世界宽）。</summary>
        public double HeatGridOffsetX
        {
            get => _heatGridOffsetX;
            set => SetProperty(ref _heatGridOffsetX, value);
        }

        private double _heatGridOffsetY;
        /// <summary>热力网格原点的归一化偏移（相对数据包围盒左上角，正 = 下移；1.0 = 世界高）。</summary>
        public double HeatGridOffsetY
        {
            get => _heatGridOffsetY;
            set => SetProperty(ref _heatGridOffsetY, value);
        }

        private bool _heatBySovereignty;
        /// <summary>热力是否按主权联盟聚合（联盟疆域凸包色块），false = 几何等距格子。</summary>
        public bool HeatBySovereignty
        {
            get => _heatBySovereignty;
            set => SetProperty(ref _heatBySovereignty, value);
        }

        private bool _showSovShading = true;
        /// <summary>主权着色模式的疆域晕染层是否显示（关闭后仅保留联盟色圆点与主权名标签）。</summary>
        public bool ShowSovShading
        {
            get => _showSovShading;
            set => SetProperty(ref _showSovShading, value);
        }
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
