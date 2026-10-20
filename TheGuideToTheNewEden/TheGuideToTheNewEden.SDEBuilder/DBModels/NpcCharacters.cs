using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("npcCharacters")]
    [SugarIndex("index_npcCharacters_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_npcCharacters_corporationID", nameof(CorporationID), OrderByType.Asc)]
    public class NpcCharacters
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        public int BloodlineID { get; set; }
        public int CorporationID { get; set; }
        public int RaceID { get; set; }
        public bool Gender { get; set; }
        public bool Ceo { get; set; }
        public long LocationID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string StartDate { get; set; }
        public bool UniqueName { get; set; }
        public NpcCharacters() { }
        public NpcCharacters(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.NpcCharacters;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            BloodlineID = data.BloodlineID;
            CorporationID = data.CorporationID;
            RaceID = data.RaceID;
            Gender = data.Gender;
            Ceo = data.Ceo;
            LocationID = data.LocationID;
            StartDate = data.StartDate;
            UniqueName = data.UniqueName;
        }
    }
}
