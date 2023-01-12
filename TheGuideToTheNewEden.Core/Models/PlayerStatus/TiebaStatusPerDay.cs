using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlayerStatus
{
    public class TiebaStatusPerDay
    {
        public DateTime Day { get; set; }
        public int NewThemes { get; set; }
        public int NewReplys { get; set; }
        public int NewMember { get; set; }
    }
}
