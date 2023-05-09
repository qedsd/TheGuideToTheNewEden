using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillItem
    {
        public ESI.NET.Models.Skills.Skill Skill { get; set; }
        public InvType InvType { get; set; }
    }
}
