using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("metaGroups")]
    [SugarIndex("index_metaGroups_id", nameof(Id), OrderByType.Asc)]
    public class MetaGroups
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public double ColorR { get; set; }
        public double ColorG { get; set; }
        public double ColorB { get; set; }
        public MetaGroups() { }
        public MetaGroups(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.MetaGroups;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            ColorR = data.Color?.R ?? 0;
            ColorG = data.Color?.G ?? 0;
            ColorB = data.Color?.B ?? 0;
        }
    }
}
