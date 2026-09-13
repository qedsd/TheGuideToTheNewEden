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
    }
}
