using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class GameLogInfoSetting : ObservableObject
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
        private bool systemNotify = true;
        public bool SystemNotify
        {
            get => systemNotify; set => SetProperty(ref systemNotify, value);
        }

        public ObservableCollection<string> keys;
        public ObservableCollection<string> Keys
        {
            get => keys; set => SetProperty(ref keys, value);
        }
    }
}
