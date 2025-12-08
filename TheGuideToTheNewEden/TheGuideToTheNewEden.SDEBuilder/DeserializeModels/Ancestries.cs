using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Ancestries : BaseModel
    {
        public int BloodlineID { get; set; }
        public int Perception { get; set; }
        public int Willpower { get; set; }
        public int Charisma { get; set; }
        public int Memory { get; set; }
        public int Intelligence { get; set; }
        public int IconID { get; set; }
        public string ShortDescription { get; set; }
    }
}
