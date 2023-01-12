using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class Skill
    {
        public List<SkillItem> Skills { get; set; }
        public long Total_sp { get; set; }
        public int Unallocated_sp { get; set; }
        public List<SkillGroup> SkillGroups { get; set; }
    }
}
