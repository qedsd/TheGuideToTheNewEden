using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("archetypes")]
    [SugarIndex("index_archetypes_id", nameof(Id), OrderByType.Asc)]
    public class Archetypes
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Title { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public Archetypes() { }
        public Archetypes(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.Archetypes;
            Id = data.Id;
            Title = data.Title?.GetValue(language);
            Description = data.Descriptions?.GetValue(language);
        }
    }
}
