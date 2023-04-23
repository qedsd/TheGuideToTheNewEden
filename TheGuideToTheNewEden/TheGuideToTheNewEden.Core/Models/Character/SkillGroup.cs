using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillGroup
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public HashSet<int> SkillIds { get; set; }
        public List<SkillItem> Skills { get; set; }
    }
}
