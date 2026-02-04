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
