namespace TheGuideToTheNewEden.SDEBuilder.DeserializeModels
{
    public class NpcCharacters : BaseModel
    {
        public int BloodlineID { get; set; }
        public bool Ceo { get; set; }
        public int CorporationID { get; set; }
        public bool Gender { get; set; }
        public long LocationID { get; set; }
        public int RaceID { get; set; }
        public string StartDate { get; set; }
        public bool UniqueName { get; set; }
    }
}
