using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SqlSugar.SugarTable("marketGroups")]
    public class MarketGroups
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [SqlSugar.SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public int? ParentGroupID { get; set; }
        public bool HasTypes { get; set; }
        public int IconID { get; set; }
        public MarketGroups() { }
        public MarketGroups(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var marketGroups = model as DeserializeModels.MarketGroups;
            Id = marketGroups.Id;
            Name = marketGroups.Names.GetValue(language);
            Description = marketGroups.Descriptions?.GetValue(language);
            ParentGroupID = marketGroups.ParentGroupID;
            HasTypes = marketGroups.HasTypes;
            IconID = marketGroups.IconID;
        }
    }
}
