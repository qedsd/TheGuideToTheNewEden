using System.Text;
using Newtonsoft.Json;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("factions")]
    [SugarIndex("index_factions_id", nameof(Id), OrderByType.Asc)]
    public class Factions
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        [SugarColumn(IsNullable = true)]
        public string ShortDescription { get; set; }
        public int CorporationID { get; set; }
        public int MilitiaCorporationID { get; set; }
        public int SolarSystemID { get; set; }
        public int IconID { get; set; }
        public double SizeFactor { get; set; }
        public bool UniqueName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string MemberRaces { get; set; }
        public Factions() { }
        public Factions(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.Factions;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            Description = data.Descriptions?.GetValue(language);
            ShortDescription = data.ShortDescription?.GetValue(language);
            CorporationID = data.CorporationID;
            MilitiaCorporationID = data.MilitiaCorporationID;
            SolarSystemID = data.SolarSystemID;
            IconID = data.IconID;
            SizeFactor = data.SizeFactor;
            UniqueName = data.UniqueName;
            MemberRaces = data.MemberRaces == null ? null : JsonConvert.SerializeObject(data.MemberRaces);
        }
    }
}
