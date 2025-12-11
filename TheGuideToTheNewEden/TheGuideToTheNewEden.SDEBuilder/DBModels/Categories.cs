using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SqlSugar.SugarTable("categories")]
    public class Categories
    {
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
