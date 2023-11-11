using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class GameLogInfo: ObservableObject
    {
        public string FilePath { get; set; }
        public int ListenerID { get; set; }
        public string ListenerName { get; set; }
        public DateTime StartTime { get; set; }
        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }

        public List<Core.Models.EVELogs.GameLogContent> LogContents { get; set; } = new List<EVELogs.GameLogContent>();
        public string Folder()
        {
            return System.IO.Path.GetDirectoryName(FilePath);
        }
        public bool IsValid()
        {
            return ListenerID > 0 && !string.IsNullOrEmpty(ListenerName) && StartTime != DateTime.MinValue;
        }
    }
}
