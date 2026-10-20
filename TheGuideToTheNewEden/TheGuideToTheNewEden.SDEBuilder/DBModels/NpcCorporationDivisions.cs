using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("npcCorporationDivisions")]
    [SugarIndex("index_npcCorporationDivisions_id", nameof(Id), OrderByType.Asc)]
    public class NpcCorporationDivisions
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string InternalName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string LeaderTypeName { get; set; }
        public NpcCorporationDivisions() { }
        public NpcCorporationDivisions(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.NpcCorporationDivisions;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            DisplayName = data.DisplayName;
            InternalName = data.InternalName;
            LeaderTypeName = data.LeaderTypeName?.GetValue(language);
        }
    }
}
