using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SqlSugar.SugarTable("groups")]
    public class Groups 
    {
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
