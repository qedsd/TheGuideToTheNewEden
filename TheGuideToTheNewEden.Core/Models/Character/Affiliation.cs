using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class Affiliation
    {
        public int Alliance_id { get; set; }
        public int Character_id { get; set; }
        public int Corporation_id { get; set; }
        public int Faction_id { get; set; }

        public CharacterInfo CharacterInfo { get; set; }
        public Corporation.Corporation Corporation { get; set; }
        public Alliance.Alliance Alliance { get; set; }
    }
}
