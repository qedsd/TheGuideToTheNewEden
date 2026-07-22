using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("dogmaAttributeCategories")]
    [SugarIndex("index_dogmaAttributeCategories_id", nameof(Id), OrderByType.Asc)]
    public class DogmaAttributeCategories
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Name { get; set; }
        [SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public DogmaAttributeCategories() { }
        public DogmaAttributeCategories(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.DogmaAttributeCategories;
            Id = data.Id;
            Name = data.Names ?? data.Name;
            Description = data.Descriptions ?? data.Description;
        }
    }
}
