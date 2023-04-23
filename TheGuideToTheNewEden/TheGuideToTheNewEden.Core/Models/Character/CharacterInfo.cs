using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class CharacterInfo
    {
        public int Ancestry_id { get; set; }
        public DateTime Birthday { get; set; }
        public int Bloodline_id { get; set; }
        public int Corporation_id { get; set; }
        public string Description { get; set; }
        public string Gender { get; set; }
        public string Name { get; set; }
        public int Race_id { get; set; }
        public double Security_status { get; set; }
    }
}
