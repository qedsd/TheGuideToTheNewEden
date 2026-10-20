using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("compressibleTypes")]
    [SugarIndex("index_compressibleTypes_id", nameof(Id), OrderByType.Asc)]
    public class CompressibleTypes
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public int CompressedTypeID { get; set; }
        public CompressibleTypes() { }
        public CompressibleTypes(DeserializeModels.BaseModel model)
        {
            var data = model as DeserializeModels.CompressibleTypes;
            Id = data.Id;
            CompressedTypeID = data.CompressedTypeID;
        }
    }
}
