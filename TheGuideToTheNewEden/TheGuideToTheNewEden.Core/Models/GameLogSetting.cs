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
