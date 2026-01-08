using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("groups")]
    [SugarIndex("index_groups_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_groups_categoryID", nameof(CategoryID), OrderByType.Asc)]
    [SugarIndex("index_groups_name", nameof(Name), OrderByType.Asc)]
    public class Groups 
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public int CategoryID { get; set; }
        public int IconID { get; set; }
        public bool UseBasePrice { get; set; }
        public bool Anchored { get; set; }
        public bool Anchorable { get; set; }
        public bool FittableNonSingleton { get; set; }
        public bool Published { get; set; }
        public Groups() { }
        public Groups(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            DeserializeModels.Groups groups = model as DeserializeModels.Groups;
            Id = groups.Id;
            Name = groups.Names.GetValue(language);
            CategoryID = groups.CategoryID;
            IconID = groups.IconID;
            UseBasePrice = groups.UseBasePrice;
            Anchored = groups.Anchored;
            Anchorable = groups.Anchorable;
            FittableNonSingleton = groups.FittableNonSingleton;
            Published = groups.Published;
        }
    }
}
