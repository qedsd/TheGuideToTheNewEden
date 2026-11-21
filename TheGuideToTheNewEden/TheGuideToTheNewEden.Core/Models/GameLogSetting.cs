using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class GameLogSetting : ObservableObject
    {
        public int ListenerID { get; set; }

        private bool _combatEnabled;
        /// <summary>战斗日志（遥修/电子战）开关（阶段 357）；打开后日志监控会为该角色启动战斗日志会话并常开小窗。</summary>
        public bool CombatEnabled
        {
            get => _combatEnabled; set => SetProperty(ref _combatEnabled, value);
        }

        private bool _showShipInTitle = true;
        /// <summary>阶段 359：日志监控窗口标题里显示当前舰船类型（ESI 读取，取不到时只显示角色名）。</summary>
        public bool ShowShipInTitle
        {
            get => _showShipInTitle; set => SetProperty(ref _showShipInTitle, value);
        }

        private int _windowOpacity = 100;
        /// <summary>
        /// 阶段 361：日志监控窗口背板不透明度（1–100，100 = 完全不透明）。
        /// 只作用于窗口背板，文字与卡片始终实色（参考频道预警小窗的"不透明度"）。
        /// </summary>
        public int WindowOpacity
        {
            get => _windowOpacity;
            set => SetProperty(ref _windowOpacity, Math.Clamp(value, 1, 100));
        }

        private double? _windowLeft;
        /// <summary>日志监控窗口上次关闭时的位置 X（WPF DIP；null 表示首次使用）。</summary>
        public double? WindowLeft
        {
            get => _windowLeft;
            set => SetProperty(ref _windowLeft, IsFinite(value) ? value : null);
        }

        private double? _windowTop;
        /// <summary>日志监控窗口上次关闭时的位置 Y（WPF DIP；null 表示首次使用）。</summary>
        public double? WindowTop
        {
            get => _windowTop;
            set => SetProperty(ref _windowTop, IsFinite(value) ? value : null);
        }

        private double? _windowWidth;
        /// <summary>日志监控窗口上次关闭时的宽度（WPF DIP；null 表示首次使用）。</summary>
        public double? WindowWidth
        {
            get => _windowWidth;
            set => SetProperty(ref _windowWidth, IsFinite(value) && value >= 240 ? value : null);
        }

        private double? _windowHeight;
        /// <summary>日志监控窗口上次关闭时的高度（WPF DIP；null 表示首次使用）。</summary>
        public double? WindowHeight
        {
            get => _windowHeight;
            set => SetProperty(ref _windowHeight, IsFinite(value) && value >= 120 ? value : null);
        }

        private static bool IsFinite(double? value)
            => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
        private ObservableCollection<GameLogItemConfig> _itemConfigs = new ObservableCollection<GameLogItemConfig>();
        public ObservableCollection<GameLogItemConfig> ItemConfigs
        {
            get => _itemConfigs; set => SetProperty(ref _itemConfigs, value);
        }
    }

    public class GameLogItemConfig : ObservableObject
    {
        [JsonIgnore]
        public string GUID {  get;} = Guid.NewGuid().ToString();

        /// <summary>
        /// 0 = 游戏日志
        /// 1 = 异常日志
        /// </summary>
        public int LogType {  get; set; }

        private string _configName;
        public string ConfigName
        {
            get => _configName; set => SetProperty(ref _configName, value);
        }

        private bool _windowNotify = true;
        public bool WindowNotify
        {
            get => _windowNotify; set => SetProperty(ref _windowNotify, value);
        }

        private bool _soundNotify = true;
        public bool SoundNotify
        {
            get => _soundNotify; set => SetProperty(ref _soundNotify, value);
        }

        private string _soundFile;
        public string SoundFile
        {
            get => _soundFile; set => SetProperty(ref _soundFile, value);
        }

        private bool _repeatSound = false;
        public bool RepeatSound
        {
            get => _repeatSound; set => SetProperty(ref _repeatSound, value);
        }

        private bool _systemNotify = true;
        public bool SystemNotify
        {
            get => _systemNotify; set => SetProperty(ref _systemNotify, value);
        }

        private int _monitorMode = 0;
        /// <summary>
        /// 0:出现关键词通知
        /// 1:停止出现关键词通知
        /// </summary>
        public int MonitorMode
        {
            get => _monitorMode; set => SetProperty(ref _monitorMode, value);
        }

        private double _disappearDelay = 30;
        /// <summary>
        /// 停止出现关键词延时
        /// </summary>
        public double DisappearDelay
        {
            get => _disappearDelay; set => SetProperty(ref _disappearDelay, value);
        }

        
        private ObservableCollection<GameLogMonityKey> _keys = new ObservableCollection<GameLogMonityKey>();
        public ObservableCollection<GameLogMonityKey> Keys
        {
            get => _keys; set => SetProperty(ref _keys, value);
        }

        public GameLogItemConfig() { }
        public GameLogItemConfig(int logType)
        {
            LogType = logType;
        }
    }
    public class GameLogMonityKey : ObservableObject
    {
        public GameLogMonityKey(string pattern)
        {
            Pattern = pattern;
        }
        private string pattern;
        public string Pattern { get => pattern; set => SetProperty(ref pattern, value);}
    }
}
