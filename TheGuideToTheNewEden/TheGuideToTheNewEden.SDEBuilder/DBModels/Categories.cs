using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("categories")]
    [SugarIndex("index_categories_id", nameof(Id), OrderByType.Asc)]
    public class Categories
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Published { get; set; }
        public Categories() { }
        public Categories(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var categories = model as DeserializeModels.Categories;
            Id = categories.Id;
            Name = categories.Names.GetValue(language);
            Published = categories.Published;
        }
    }
}
