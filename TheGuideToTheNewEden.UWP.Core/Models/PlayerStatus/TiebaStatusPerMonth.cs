using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlayerStatus
{
    public class TiebaStatusPerMonth
    {
        public int Month { get; set; }
        public int NewThemes { get; set; }
        public int NewReplys { get; set; }
        public int NewMembers { get; set; }
    }
}
