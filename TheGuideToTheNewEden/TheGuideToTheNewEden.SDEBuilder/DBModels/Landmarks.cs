using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("landmarks")]
    [SugarIndex("index_landmarks_id", nameof(Id), OrderByType.Asc)]
    public class Landmarks
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public Landmarks() { }
        public Landmarks(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.Landmarks;
            Id = data.Id;
            Name = data.Names?.GetValue(language);
            Description = data.Descriptions?.GetValue(language);
            if (data.Position != null)
            {
                X = data.Position.X;
                Y = data.Position.Y;
                Z = data.Position.Z;
            }
        }
    }
}
