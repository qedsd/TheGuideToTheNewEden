using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KBSetting
    {
        public bool AutoConnect { get; set; } = true;
        public bool Notify { get; set; } = true;
        public long MinNotifyValue { get; set; } = 1000000000;
        public HashSet<int> Types { get; set; }
        public HashSet<int> Systems { get; set; }
        public HashSet<int> Regions { get; set; }
        public HashSet<int> Characters { get; set; }
        public HashSet<int> Corps { get; set; }
        public HashSet<int> Alliances { get; set; }
        public int MaxKBItems { get; set; } = 100;
    }
}
