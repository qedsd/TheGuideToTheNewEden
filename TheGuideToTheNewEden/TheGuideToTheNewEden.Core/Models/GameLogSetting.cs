using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class GameLogSetting : ObservableObject
    {
        public int ListenerID { get; set; }
        private bool windowNotify = true;
        public bool WindowNotify
        {
            get => windowNotify; set => SetProperty(ref windowNotify, value);
        }
        private bool soundNotify = true;
        public bool SoundNotify
        {
            get => soundNotify; set => SetProperty(ref soundNotify, value);
        }
        private string soundFile;
        public string SoundFile
        {
            get => soundFile; set => SetProperty(ref soundFile, value);
        }
        private bool systemNotify = true;
        public bool SystemNotify
        {
            get => systemNotify; set => SetProperty(ref systemNotify, value);
        }

        private int monitorMode = 0;
        /// <summary>
        /// 0:出现关键词通知
        /// 1:停止出现关键词通知
        /// </summary>
        public int MonitorMode
        {
            get => monitorMode; set => SetProperty(ref monitorMode, value);
        }

        private double disappearDelay = 10;
        /// <summary>
        /// 停止出现关键词延时
        /// </summary>
        public double DisappearDelay
        {
            get => disappearDelay; set => SetProperty(ref disappearDelay, value);
        }

        private bool repeatSound = false;
        public bool RepeatSound
        {
            get => repeatSound; set => SetProperty(ref repeatSound, value);
        }

        private ObservableCollection<GameLogMonityKey> keys = new ObservableCollection<GameLogMonityKey>();
        public ObservableCollection<GameLogMonityKey> Keys
        {
            get => keys; set => SetProperty(ref keys, value);
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
