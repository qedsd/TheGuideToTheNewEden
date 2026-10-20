using Newtonsoft.Json;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("races")]
    [SugarIndex("index_races_id", nameof(Id), OrderByType.Asc)]
    public class Races
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public int IconID { get; set; }
        public int ShipTypeID { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Skills { get; set; }
        public Races() { }
        public Races(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.Races;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            Description = data.Descriptions?.GetValue(language);
            IconID = data.IconID;
            ShipTypeID = data.ShipTypeID;
            Skills = data.Skills == null ? null : JsonConvert.SerializeObject(data.Skills);
        }
    }
}
