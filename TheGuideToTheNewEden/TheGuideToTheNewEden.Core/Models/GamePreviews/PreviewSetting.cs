using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class PreviewSetting : ObservableObject
    {
        private string processeKeywords = "exefile";
        /// <summary>
        /// 游戏进程名关键词
        /// 按,分割
        /// </summary>
        public string ProcessKeywords
        {
            get => processeKeywords;
            set => SetProperty(ref processeKeywords, value);
        }

        private string switchHotkey_Forward = "F5";
        /// <summary>
        /// 默认快捷键：切换运行中的窗口对应的源窗口
        /// </summary>
        public string SwitchHotkey_Forward
        {
            get => switchHotkey_Forward;
            set => SetProperty(ref switchHotkey_Forward, value);
        }

        private string switchHotkey_Backward = "F4";
        /// <summary>
        /// 默认快捷键：切换运行中的窗口对应的源窗口
        /// </summary>
        public string SwitchHotkey_Backward
        {
            get => switchHotkey_Backward;
            set => SetProperty(ref switchHotkey_Backward, value);
        }

        public ObservableCollection<PreviewHotKeyGroup> HotKeyGroups { get; set; } = new ObservableCollection<PreviewHotKeyGroup>();

        private List<PreviewItem> previewItems = new List<PreviewItem>();
        public List<PreviewItem> PreviewItems
        {
            get => previewItems;
            set => SetProperty(ref previewItems, value);
        }

        private int autoLayout;
        /// <summary>
        /// 窗口布局对齐方式
        /// </summary>
        public int AutoLayout
        {
            get => autoLayout;
            set
            {
                SetProperty(ref autoLayout, value);
            }
        }
        private int autoLayoutAnchor;
        /// <summary>
        /// 窗口布局对齐位置
        /// </summary>
        public int AutoLayoutAnchor
        {
            get => autoLayoutAnchor;
            set
            {
                SetProperty(ref autoLayoutAnchor, value);
            }
        }

        private int autoLayoutSpan = 10;
        /// <summary>
        /// 自动对齐间隔
        /// </summary>
        public int AutoLayoutSpan
        {
            get => autoLayoutSpan;
            set
            {
                SetProperty(ref autoLayoutSpan, value);
            }
        }

        private int autoLayoutCount = 10;
        /// <summary>
        /// 行/列最大窗口个数
        /// </summary>
        public int AutoLayoutCount
        {
            get => autoLayoutCount;
            set
            {
                SetProperty(ref autoLayoutCount, value);
            }
        }

        private bool snapEnabled = true;
        /// <summary>
        /// 拖动预览窗口时是否吸附到其它可见预览窗口（边/角对齐，可沿对齐边滑动）。
        /// </summary>
        public bool SnapEnabled
        {
            get => snapEnabled;
            set
            {
                SetProperty(ref snapEnabled, value);
            }
        }

        private int snapDistance = 12;
        /// <summary>
        /// 吸附距离（物理像素）：窗口边与目标边的距离小于等于它时吸附。
        /// </summary>
        public int SnapDistance
        {
            get => snapDistance;
            set
            {
                SetProperty(ref snapDistance, value);
            }
        }

        private int uniformHeight = 300;
        /// <summary>
        /// 统一窗口长度
        /// </summary>
        public int UniformHeight
        {
            get => uniformHeight;
            set
            {
                SetProperty(ref uniformHeight, value);
            }
        }
        private int uniformWidth = 533;
        /// <summary>
        /// 统一窗口宽度
        /// </summary>
        public int UniformWidth
        {
            get => uniformWidth;
            set
            {
                SetProperty(ref uniformWidth, value);
            }
        }

        private bool startAllWithNoneSetting = true;
        /// <summary>
        /// 开始全部时包含无保存设置的进程
        /// </summary>
        public bool StartAllWithNoneSetting
        {
            get => startAllWithNoneSetting;
            set
            {
                SetProperty(ref startAllWithNoneSetting, value);
            }
        }

        private int startAllDefaultLoadType = 0;
        /// <summary>
        /// 开始全部无保存配置的进程加载配置方式
        /// 0 依序使用已保存列表
        /// 1 新建默认
        /// </summary>
        public int StartAllDefaultLoadType
        {
            get => startAllDefaultLoadType;
            set
            {
                SetProperty(ref startAllDefaultLoadType, value);
            }
        }

        private int setForegroundWindowMode = 4;
        public int SetForegroundWindowMode
        {
            get => setForegroundWindowMode;
            set
            {
                SetProperty(ref setForegroundWindowMode, value);
            }
        }

        /// <summary>
        /// 所有启动过的进程均按顺序保存至此
        /// 而不仅是上一回检测到的进程
        /// </summary>
        public List<string> ProcessOrder { get; set; } = new List<string>();

        private bool _autoStartNewProcess = true;
        /// <summary>
        /// 自动开始新进程预览
        /// </summary>
        public bool AutoStartNewProcess
        {
            get => _autoStartNewProcess;
            set
            {
                SetProperty(ref _autoStartNewProcess, value);
            }
        }

        private bool _switchCharacterKeepSetting = true;
        /// <summary>
        /// 同进程切换角色沿用切换前的设置
        /// </summary>
        public bool SwitchCharacterKeepSetting
        {
            get => _switchCharacterKeepSetting;
            set
            {
                SetProperty(ref _switchCharacterKeepSetting, value);
            }
        }

        private int _dragMouseButton = 0;
        /// <summary>
        /// 拖动预览窗口用的鼠标键：0 = 左键（默认），1 = 右键。
        /// </summary>
        public int DragMouseButton
        {
            get => _dragMouseButton;
            set
            {
                SetProperty(ref _dragMouseButton, value < 0 || value > 1 ? 0 : value);
            }
        }

        private int _activateMouseButton = 0;
        /// <summary>
        /// 点击（不拖动）激活对应游戏窗口用的鼠标键：0 = 左键（默认），1 = 右键。
        /// </summary>
        public int ActivateMouseButton
        {
            get => _activateMouseButton;
            set
            {
                SetProperty(ref _activateMouseButton, value < 0 || value > 1 ? 0 : value);
            }
        }

        private bool _useNativeThumbnailOverlay = true;
        /// <summary>
        /// 使用独立的原生 Win32 layered 画面层显示 DWM 缩略图。
        /// 默认开启；关闭后回退到“DWM 缩略图自身 opacity + WPF 玻璃”的旧实现。
        /// </summary>
        public bool UseNativeThumbnailOverlay
        {
            get => _useNativeThumbnailOverlay;
            set
            {
                SetProperty(ref _useNativeThumbnailOverlay, value);
            }
        }

        private bool _showIntelAlert = true;
        /// <summary>
        /// 多开预览窗叠加层的**全局总开关**（阶段 355）：频道预警信息行。
        /// <para>
        /// 关闭时**覆盖所有单项设置**（即使某项打开了也不显示）；打开时由每项的
        /// <see cref="PreviewItem.ShowIntelAlert"/> 决定。
        /// </para>
        /// </summary>
        public bool ShowIntelAlert
        {
            get => _showIntelAlert;
            set => SetProperty(ref _showIntelAlert, value);
        }

        private bool _showCombatInfo = true;
        /// <summary>
        /// 多开预览窗叠加层的**全局总开关**（阶段 357）：战斗日志信息行（反跳/毁电/遥修/承伤 DPS）。
        /// <para>关闭时覆盖所有单项设置；打开时由每项的 <see cref="PreviewItem.ShowCombatInfo"/> 决定。</para>
        /// </summary>
        public bool ShowCombatInfo
        {
            get => _showCombatInfo;
            set => SetProperty(ref _showCombatInfo, value);
        }

        private bool _showMonitorHits;
        /// <summary>
        /// 多开预览窗叠加层的可选开关（阶段 357）：把**日志监控关键词命中**（如"诱导检测"）也显示成一行。
        /// <para>默认关闭；命中后保留 30 秒。战斗日志（<see cref="ShowCombatInfo"/>）是另一套开关。</para>
        /// </summary>
        public bool ShowMonitorHits
        {
            get => _showMonitorHits;
            set => SetProperty(ref _showMonitorHits, value);
        }
    }
    public class PreviewHotKeyGroup : ObservableObject
    {
        private string switchHotkey_Forward;
        public string SwitchHotkey_Forward
        {
            get => switchHotkey_Forward;
            set => SetProperty(ref switchHotkey_Forward, value);
        }

        private string switchHotkey_Backward;
        public string SwitchHotkey_Backward
        {
            get => switchHotkey_Backward;
            set => SetProperty(ref switchHotkey_Backward, value);
        }

        private string groupName = "Group";
        public string GroupName
        {
            get => groupName;
            set => SetProperty(ref groupName, value);
        }

        public HashSet<string> GameNames { get; set; } = new HashSet<string>();

        private bool _useGlobalOrder = true;
        /// <summary>
        /// 分组热键的排序方式：true = 跟随「切换顺序」里的全局顺序；
        /// false = 使用 <see cref="Order"/> 里的单独排序。
        /// </summary>
        public bool UseGlobalOrder
        {
            get => _useGlobalOrder;
            set => SetProperty(ref _useGlobalOrder, value);
        }

        /// <summary>
        /// 单独排序时的角色名顺序（仅 <see cref="UseGlobalOrder"/> 为 false 时生效）。
        /// 名单里没有的成员会由 UI 自动追加到末尾；移出分组的名字在下次重建时清理。
        /// </summary>
        public List<string> Order { get; set; } = new List<string>();

        [JsonIgnore]

        public int ForwardHotkeyRegisterId { get; set; }

        [JsonIgnore]

        public int BackwardHotkeyRegisterId { get; set; }
        [JsonIgnore]
        public string LastActiveProcessGUID { get; set; }

        private bool _restart = true;
        /// <summary>
        /// 从别的分组切换回来时是否从头开始显示
        /// </summary>
        public bool Restart
        {
            get => _restart;
            set => SetProperty(ref _restart, value);
        }
    }
}
