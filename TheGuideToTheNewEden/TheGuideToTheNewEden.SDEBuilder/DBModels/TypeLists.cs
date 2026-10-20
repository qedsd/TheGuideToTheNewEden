using Newtonsoft.Json;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("typeLists")]
    [SugarIndex("index_typeLists_id", nameof(Id), OrderByType.Asc)]
    public class TypeLists
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string IncludedTypeIDs { get; set; }
        public TypeLists() { }
        public TypeLists(DeserializeModels.BaseModel model)
        {
            var data = model as DeserializeModels.TypeLists;
            Id = data.Id;
            Name = data.Name ?? data.Names;
            IncludedTypeIDs = data.IncludedTypeIDs == null ? null : JsonConvert.SerializeObject(data.IncludedTypeIDs);
        }
    }
}
