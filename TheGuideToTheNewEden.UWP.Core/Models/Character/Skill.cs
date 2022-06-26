using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Character
{
    public class Skill
    {
        public List<Skill> Skills { get; set; }
        public long Total_sp { get; set; }
        public int Unallocated_sp { get; set; }
    }
}
