using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class KBSetting
    {
        public bool AutoConnect { get; set; } = true;
        public bool Notify { get; set; } = true;
        public long MinNotifyValue { get; set; }
        public HashSet<int> Types { get; set; }
        public HashSet<int> Systems { get; set; }
        public HashSet<int> Region { get; set; }
        public HashSet<int> Characters { get; set; }
        public HashSet<int> Corps { get; set; }
        public HashSet<int> Alliances { get; set; }
    }
}
