using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Drawing;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class PreviewItem : ObservableObject
    {

        private string name;
        /// <summary>
        /// 标记id
        /// 默认为游戏角色名
        /// </summary>
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        /// <summary>
        /// 窗口不透明度
        /// 0-100
        /// </summary>
        private int overlapOpacity = 100;
        public int OverlapOpacity
        {
            get => overlapOpacity;
            set => SetProperty(ref overlapOpacity, value);
        }

        private int winX = 100;
        public int WinX
        {
            get => winX;
            set => SetProperty(ref winX, value);
        }

        private int winY = 100;
        public int WinY
        {
            get => winY;
            set => SetProperty(ref winY, value);
        }

        private int winW = 533;
        public int WinW
        {
            get => winW;
            set => SetProperty(ref winW, value);
        }

        private int winH = 300;
        public int WinH
        {
            get => winH;
            set => SetProperty(ref winH, value);
        }

        private bool _cropEnabled;
        /// <summary>
        /// 是否只显示游戏画面的某个自定义区域（阶段 347）。
        /// 关闭时为旧行为：显示源窗口客户区的全部内容。
        /// </summary>
        public bool CropEnabled
        {
            get => _cropEnabled;
            set => SetProperty(ref _cropEnabled, value);
        }

        private int _cropX;
        /// <summary>自定义区域左上角 X（源窗口客户区像素）。</summary>
        public int CropX
        {
            get => _cropX;
            set => SetProperty(ref _cropX, value);
        }

        private int _cropY;
        /// <summary>自定义区域左上角 Y（源窗口客户区像素）。</summary>
        public int CropY
        {
            get => _cropY;
            set => SetProperty(ref _cropY, value);
        }

        private int _cropW;
        /// <summary>自定义区域宽（源窗口客户区像素）；0 表示未设置。</summary>
        public int CropW
        {
            get => _cropW;
            set => SetProperty(ref _cropW, value);
        }

        private int _cropH;
        /// <summary>自定义区域高（源窗口客户区像素）；0 表示未设置。</summary>
        public int CropH
        {
            get => _cropH;
            set => SetProperty(ref _cropH, value);
        }

        /// <summary>是否真的需要裁剪（开关打开且有有效区域）。</summary>
        [JsonIgnore]
        public bool HasCrop => CropEnabled && CropW > 0 && CropH > 0;

        /// <summary>清空自定义区域（恢复显示全部画面）。</summary>
        public void ClearCrop()
        {
            CropEnabled = false;
            CropX = 0;
            CropY = 0;
            CropW = 0;
            CropH = 0;
        }

        private bool lockAspectRatio = true;
        /// <summary>
        /// 拖拽边框 / 滚轮缩放预览窗口时是否锁定画面区域的横纵比例（默认锁住）。
        /// 关掉后窗口可自由改成任意宽高比，游戏画面按原比例居中留边。
        /// </summary>
        public bool LockAspectRatio
        {
            get => lockAspectRatio;
            set => SetProperty(ref lockAspectRatio, value);
        }

        private string hotKey;
        /// <summary>
        /// 快捷键
        /// 以+分隔
        /// </summary>
        public string HotKey
        {
            get => hotKey;
            set => SetProperty(ref hotKey, value);
        }
        private ProcessInfo processInfo;
        [JsonIgnore]
        public ProcessInfo ProcessInfo
        {
            get => processInfo; set => SetProperty(ref processInfo, value);
        }

        private bool hideOnForeground = false;
        public bool HideOnForeground
        {
            get => hideOnForeground;
            set => SetProperty(ref hideOnForeground, value);
        }

        private bool highlight = true;
        /// <summary>
        /// 激活游戏窗口、管理窗口选中时高亮预览窗口
        /// </summary>
        public bool Highlight
        {
            get => highlight;
            set => SetProperty(ref highlight, value);
        }
        private Color highlightColor = Color.Green;
        public Color HighlightColor
        {
            get => highlightColor;
            set => SetProperty(ref highlightColor, value);
        }

        private Color _titleHighlightColor = Color.Green;
        public Color TitleHighlightColor
        {
            get => _titleHighlightColor;
            set => SetProperty(ref _titleHighlightColor, value);
        }

        private Color _titleNormalColor = Color.Green;
        public Color TitleNormalColor
        {
            get => _titleNormalColor;
            set => SetProperty(ref _titleNormalColor, value);
        }

        private double highlightMarginLeft = 4;
        public double HighlightMarginLeft
        {
            get => highlightMarginLeft;
            set => SetProperty(ref highlightMarginLeft, value);
        }

        private double highlightMarginTop = 4;
        public double HighlightMarginTop
        {
            get => highlightMarginTop;
            set => SetProperty(ref highlightMarginTop, value);
        }

        private double highlightMarginRight = 4;
        public double HighlightMarginRight
        {
            get => highlightMarginRight;
            set => SetProperty(ref highlightMarginRight, value);
        }

        private double highlightMarginBottom = 4;
        public double HighlightMarginBottom
        {
            get => highlightMarginBottom;
            set => SetProperty(ref highlightMarginBottom, value);
        }

        private string nameOverlayFontFamily = "Microsoft YaHei UI";
        /// <summary>
        /// 预览窗口左上角"角色名"叠加的字体
        /// </summary>
        public string NameOverlayFontFamily
        {
            get => nameOverlayFontFamily;
            set => SetProperty(ref nameOverlayFontFamily, value);
        }

        private double nameOverlayFontSize = 46;
        /// <summary>
        /// 预览窗口左上角"角色名"叠加的字号（DIP，基准值；开启跟随时按缩放比例放大）
        /// </summary>
        public double NameOverlayFontSize
        {
            get => nameOverlayFontSize;
            set => SetProperty(ref nameOverlayFontSize, value);
        }

        private bool nameOverlayFollowScale = true;
        /// <summary>
        /// 角色名叠加是否跟随预览窗口缩放（拖动/滚轮放大时字号同比放大）
        /// </summary>
        public bool NameOverlayFollowScale
        {
            get => nameOverlayFollowScale;
            set => SetProperty(ref nameOverlayFollowScale, value);
        }

        private double nameOverlayScaleFactor = 1;
        /// <summary>
        /// 角色名跟随缩放的倍率（仅在 <see cref="NameOverlayFollowScale"/> 为 true 时生效）
        /// 实际字号 = <see cref="NameOverlayFontSize"/> × (窗口宽 / 基准宽) × 本倍率
        /// </summary>
        public double NameOverlayScaleFactor
        {
            get => nameOverlayScaleFactor;
            set => SetProperty(ref nameOverlayScaleFactor, value);
        }

        private Color nameOverlayBackgroundColor = Color.FromArgb(153, 0, 0, 0);
        /// <summary>
        /// 角色名叠加的背景色（正常状态，含透明度，用 <see cref="ArgbColorJsonConverter"/> 序列化成 #AARRGGBB）
        /// </summary>
        [JsonConverter(typeof(Helpers.ArgbColorJsonConverter))]
        public Color NameOverlayBackgroundColor
        {
            get => nameOverlayBackgroundColor;
            set => SetProperty(ref nameOverlayBackgroundColor, value);
        }

        private Color nameOverlayBackgroundColorHighlight = Color.Green;
        /// <summary>
        /// 角色名叠加的"第二种背景色"（含透明度）。
        /// <b>与是否高亮无关、一直按用户选择生效</b>：高亮边框另有独立的颜色设置，
        /// 这里只是把背景色也做成两项可选，便于用户为某些窗口配不同底色。
        /// 名字里的 Highlight 是沿用 WinUI 版"名称条高亮颜色"那一对的叫法。
        /// </summary>
        [JsonConverter(typeof(Helpers.ArgbColorJsonConverter))]
        public Color NameOverlayBackgroundColorHighlight
        {
            get => nameOverlayBackgroundColorHighlight;
            set => SetProperty(ref nameOverlayBackgroundColorHighlight, value);
        }

        private Color nameOverlayForegroundColor = Color.White;
        /// <summary>
        /// 角色名叠加的文字颜色（含透明度；默认白色）
        /// </summary>
        [JsonConverter(typeof(Helpers.ArgbColorJsonConverter))]
        public Color NameOverlayForegroundColor
        {
            get => nameOverlayForegroundColor;
            set => SetProperty(ref nameOverlayForegroundColor, value);
        }

        private bool respondGlobalHotKey = true;
        /// <summary>
        /// 响应全局快捷键
        /// </summary>
        public bool RespondGlobalHotKey
        {
            get => respondGlobalHotKey;
            set => SetProperty(ref respondGlobalHotKey, value);
        }

        private bool showPreviewWindow = true;
        /// <summary>
        /// 显示预览窗口
        /// </summary>
        public bool ShowPreviewWindow
        {
            get => showPreviewWindow;
            set => SetProperty(ref showPreviewWindow, value);
        }

        private int showPreviewWindowMode = 1;
        /// <summary>
        /// 显示预览窗口模式
        /// </summary>
        public int ShowPreviewWindowMode
        {
            get => showPreviewWindowMode;
            set => SetProperty(ref showPreviewWindowMode, value);
        }

        private bool showIntelAlert = true;
        /// <summary>
        /// 在该预览窗的叠加层显示**频道预警**信息行（最近 / 最新预警的跳数与多少分钟前）。
        /// <para>
        /// 实际是否显示还取决于全局总开关 <see cref="PreviewSetting.ShowIntelAlert"/>
        /// 与"该角色是否有运行中的频道预警会话"：全局关闭会覆盖本开关；没有同名会话时
        /// 开始预览会由多开页右下角弹一条提示。
        /// </para>
        /// </summary>
        public bool ShowIntelAlert
        {
            get => showIntelAlert;
            set => SetProperty(ref showIntelAlert, value);
        }

        private string alertOverlayFontFamily = "Microsoft YaHei UI";
        /// <summary>
        /// 预警信息行（角色名下方）的字体。与角色名叠加各自独立设置。
        /// </summary>
        public string AlertOverlayFontFamily
        {
            get => alertOverlayFontFamily;
            set => SetProperty(ref alertOverlayFontFamily, value);
        }

        private double alertOverlayFontSize = 30;
        /// <summary>
        /// 预警信息行的字号（DIP，基准值）。默认比角色名小一些但保证可读；
        /// 开启 <see cref="AlertOverlayFollowScale"/> 时按窗口宽度等比缩放（有下限，不会缩到看不清）。
        /// </summary>
        public double AlertOverlayFontSize
        {
            get => alertOverlayFontSize;
            set => SetProperty(ref alertOverlayFontSize, value);
        }

        private bool alertOverlayFollowScale = true;
        /// <summary>
        /// 预警信息行是否跟随预览窗口缩放（拖动/滚轮放大时字号同比放大）。
        /// </summary>
        public bool AlertOverlayFollowScale
        {
            get => alertOverlayFollowScale;
            set => SetProperty(ref alertOverlayFollowScale, value);
        }

        private double alertOverlayScaleFactor = 1;
        /// <summary>
        /// 预警信息行跟随缩放的倍率（仅在 <see cref="AlertOverlayFollowScale"/> 为 true 时生效）。
        /// </summary>
        public double AlertOverlayScaleFactor
        {
            get => alertOverlayScaleFactor;
            set => SetProperty(ref alertOverlayScaleFactor, value);
        }

        private Color alertOverlayForegroundColor = Color.White;
        /// <summary>
        /// 预警信息行的文字颜色（含透明度，阶段 356 起与角色名叠加各自独立）。
        /// </summary>
        [JsonConverter(typeof(Helpers.ArgbColorJsonConverter))]
        public Color AlertOverlayForegroundColor
        {
            get => alertOverlayForegroundColor;
            set => SetProperty(ref alertOverlayForegroundColor, value);
        }

        private Color alertOverlayBackgroundColor = Color.FromArgb(153, 0, 0, 0);
        /// <summary>
        /// 预警信息行的背景色（含透明度；阶段 356 起与角色名叠加各自独立）。
        /// <para>只有一种底色（不随"客户端到前台"切换），避免预警行在切窗口时突然变色、影响判读。</para>
        /// </summary>
        [JsonConverter(typeof(Helpers.ArgbColorJsonConverter))]
        public Color AlertOverlayBackgroundColor
        {
            get => alertOverlayBackgroundColor;
            set => SetProperty(ref alertOverlayBackgroundColor, value);
        }
    }
}
