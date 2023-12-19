using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class ChannelMonitorItem : ObservableObject
    {
        public string Name { get; set; }

        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }

        private ChannelMonitorSetting setting;
        public ChannelMonitorSetting Setting
        {
            get => setting; set => SetProperty(ref setting, value);
        }

    }
    public class ChannelMonitorSetting : ObservableObject
    {
        public string Name { get; set; }
        public List<string> SelectedChannels { get; set; }

        private bool windowNotify = true;
        public bool WindowNotify
        {
            get => windowNotify; set => SetProperty(ref windowNotify, value);
        }
        private bool systemNotify = true;
        public bool SystemNotify
        {
            get => systemNotify; set => SetProperty(ref systemNotify, value);
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
        
        private bool repeatSound = false;
        public bool RepeatSound
        {
            get => repeatSound; set => SetProperty(ref repeatSound, value);
        }

        private ObservableCollection<ChannelMonitorKey> keys = new ObservableCollection<ChannelMonitorKey>();
        public ObservableCollection<ChannelMonitorKey> Keys
        {
            get => keys; set => SetProperty(ref keys, value);
        }
    }

    public class ChannelMonitorKey : ObservableObject
    {
        public ChannelMonitorKey(string pattern)
        {
            Pattern = pattern;
        }
        private string pattern;
        public string Pattern { get => pattern; set => SetProperty(ref pattern, value); }
    }
}
