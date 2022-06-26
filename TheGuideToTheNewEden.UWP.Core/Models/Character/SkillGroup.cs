using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Character
{
    public class SkillGroup
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public List<int> SkillsId { get; set; }
        public List<Skill> CHSkills { get; set; }
    }
}
