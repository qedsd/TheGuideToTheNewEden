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

        /// <summary>
        /// 星图画布显示参数（WPF 版，顶栏「显示设置」弹窗实时可调）。
        /// </summary>
        public MapDisplayConfig Display { get; set; } = new MapDisplayConfig();
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

        private bool _showLogos = true;
        /// <summary>圆点是否叠加联盟/势力徽标（顶栏「势力」开关，EVE 官方星图样式）。</summary>
        public bool ShowLogos
        {
            get => _showLogos;
            set => SetProperty(ref _showLogos, value);
        }
    }

    /// <summary>
    /// 星图画布显示参数（全部可实时调整，默认值 = 历史调优结果）。
    /// 缩放类参数的参照系：zmult = 当前缩放 / 适配全图缩放（zmult≈1 即全图视野）。
    /// </summary>
    public class MapDisplayConfig
    {
        // ---------- 节点 ----------

        /// <summary>圆点半径基数：nodeR = Base × zmult^Power，夹在 Min/Max 之间。</summary>
        public double NodeRadiusBase { get; set; } = 1.5;
        /// <summary>圆点半径的缩放幂（越大随缩放增长越快）。</summary>
        public double NodeRadiusPower { get; set; } = 0.42;
        /// <summary>圆点最小半径（全图最远视野）。</summary>
        public double NodeRadiusMin { get; set; } = 1.2;
        /// <summary>圆点最大半径（最深放大）。</summary>
        public double NodeRadiusMax { get; set; } = 26;
        /// <summary>白色内核出现的缩放档（zmult 超过该值后圆点中心叠白核）。</summary>
        public double KernelZoom { get; set; } = 30;
        /// <summary>白色内核相对圆点的比例（0.45 = 半径的 45%）。</summary>
        public double KernelScale { get; set; } = 0.45;

        // ---------- 徽标与光晕 ----------

        /// <summary>徽标显示门槛：圆点半径达到该值才叠加联盟/势力徽标并扩大圆点。</summary>
        public double LogoGate { get; set; } = 5;
        /// <summary>徽标半径 = 圆点半径 × LogoScale（容纳进圆点后圆点扩到 LogoScale×Ring）。</summary>
        public double LogoScale { get; set; } = 2.4;
        /// <summary>扩大后圆点相对徽标的倍数（>1 的部分 = 徽标外沿的模式色环宽度）。</summary>
        public double LogoRingFactor { get; set; } = 1.15;
        /// <summary>扩大圆点的光晕收紧系数（光晕半径 = 圆点半径 × 该值，与 GlowScale 取大者）。</summary>
        public double LogoGlowFactor { get; set; } = 1.35;
        /// <summary>外发光半径基倍：GlowScale = Base + (zmult-1) × Slope，夹在 Base 与 Max 之间。</summary>
        public double GlowScaleBase { get; set; } = 1.15;
        /// <summary>外发光半径随缩放的斜率。</summary>
        public double GlowScaleSlope { get; set; } = 0.45;
        /// <summary>外发光半径倍率上限。</summary>
        public double GlowScaleMax { get; set; } = 3.4;
        /// <summary>外发光透明度渐显斜率：Alpha = (zmult-1) × Slope，夹在 0 与 Max 之间。</summary>
        public double GlowAlphaSlope { get; set; } = 55;
        /// <summary>外发光透明度上限（深色主题；浅色主题自动 ×0.45）。</summary>
        public double GlowAlphaMax { get; set; } = 220;

        // ---------- 文字 ----------

        /// <summary>星系名字号斜率：字号 = Slope × zmult，夹在 Min/Max 之间。</summary>
        public double NameSizeSlope { get; set; } = 0.8;
        /// <summary>星系名最小字号。</summary>
        public double NameSizeMin { get; set; } = 9;
        /// <summary>星系名最大字号。</summary>
        public double NameSizeMax { get; set; } = 18;
        /// <summary>安等/数值字号斜率。</summary>
        public double SecSizeSlope { get; set; } = 0.55;
        /// <summary>安等/数值最小字号。</summary>
        public double SecSizeMin { get; set; } = 6;
        /// <summary>安等/数值最大字号。</summary>
        public double SecSizeMax { get; set; } = 11;
        /// <summary>名字淡入起点（zmult 低于该值不显示文字）。</summary>
        public double NameFadeStart { get; set; } = 5.5;
        /// <summary>名字淡入终点（到该缩放完全不透明；须大于起点）。</summary>
        public double NameFadeEnd { get; set; } = 7.5;
        /// <summary>名字透明度基数：Alpha = Base + zmult × Slope，夹在 Min/Max 之间。</summary>
        public double NameAlphaBase { get; set; } = 60;
        /// <summary>名字透明度随缩放的斜率。</summary>
        public double NameAlphaSlope { get; set; } = 12;
        /// <summary>名字透明度下限。</summary>
        public double NameAlphaMin { get; set; } = 80;
        /// <summary>名字透明度上限。</summary>
        public double NameAlphaMax { get; set; } = 235;
        /// <summary>文字基线离圆点外沿的距离系数（× 字号）。</summary>
        public double NameDotGap { get; set; } = 1.2;
        /// <summary>文字基线额外外边距（像素）。</summary>
        public double TextPad { get; set; } = 2;
        /// <summary>安等数值与名字的间距（像素）。</summary>
        public double SecGap { get; set; } = 4;
        /// <summary>安等数值相对名字的垂直居中系数（字面中线 ≈ 基线上方 0.35×字号）。</summary>
        public double SecAlignFactor { get; set; } = 0.35;
        /// <summary>资源/热度数值第二行的行距系数（× 安等字号）。</summary>
        public double ValueLineSpacing { get; set; } = 1.4;

        // ---------- 连线与热力 ----------

        /// <summary>星门连线淡入起点（zmult 低于该值不画连线）。</summary>
        public double LinkFadeStart { get; set; } = 1.05;
        /// <summary>连线宽度基数：宽 = Base × zmult^Power（最小 0.5）。</summary>
        public double LinkWidthBase { get; set; } = 0.75;
        /// <summary>连线宽度的缩放幂。</summary>
        public double LinkWidthPower { get; set; } = 0.25;
        /// <summary>热力格子色块透明度基数：Alpha = Base + 分位 × Slope，夹在 Base 与 Max 之间。</summary>
        public double HeatAlphaBase { get; set; } = 40;
        /// <summary>热力格子色块透明度斜率。</summary>
        public double HeatAlphaSlope { get; set; } = 150;
        /// <summary>热力格子色块透明度上限。</summary>
        public double HeatAlphaMax { get; set; } = 185;
        /// <summary>主权晕染软斑透明度基数（热力+主权聚合与主权模式共用）。</summary>
        public double SovBlobAlphaBase { get; set; } = 55;
        /// <summary>主权晕染软斑透明度斜率。</summary>
        public double SovBlobAlphaSlope { get; set; } = 130;
        /// <summary>主权晕染软斑透明度上限。</summary>
        public double SovBlobAlphaMax { get; set; } = 185;
        /// <summary>主权晕染位图整体阻尼（1 = 不衰减；位图与矢量域共用）。</summary>
        public double SovAlphaDamp { get; set; } = 0.5;
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
