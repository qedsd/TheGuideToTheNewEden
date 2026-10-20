using System.Collections.Generic;

namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class Factions : BaseModel
    {
        public int CorporationID { get; set; }
        public Languages ShortDescription { get; set; }
        public int MilitiaCorporationID { get; set; }
        public int SolarSystemID { get; set; }
        public int IconID { get; set; }
        public double SizeFactor { get; set; }
        public bool UniqueName { get; set; }
        public List<int> MemberRaces { get; set; }
        public string FlatLogo { get; set; }
        public string FlatLogoWithName { get; set; }
    }
}
