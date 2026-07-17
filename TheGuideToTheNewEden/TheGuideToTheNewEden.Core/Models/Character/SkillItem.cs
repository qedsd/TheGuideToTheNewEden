using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillItem
    {
        public EVEStandard.Models.Skill Skill { get; set; }
        public InvType InvType { get; set; }
    }
}
