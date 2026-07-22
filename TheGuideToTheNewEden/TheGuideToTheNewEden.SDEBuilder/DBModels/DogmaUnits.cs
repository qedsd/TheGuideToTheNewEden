using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("dogmaUnits")]
    [SugarIndex("index_dogmaUnits_id", nameof(Id), OrderByType.Asc)]
    public class DogmaUnits
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string DisplayName { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public DogmaUnits() { }
        public DogmaUnits(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.DogmaUnits;
            if (data == null) return;
            Id = data.Id;
            Name = data.Name;
            DisplayName = data.DisplayName?.GetValue(language);
            Description = data.Description?.GetValue(language);
        }
    }
}
