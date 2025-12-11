using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("types")]
    [SugarIndex("index_types_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_types_groupID", nameof(GroupID), OrderByType.Asc)]
    [SugarIndex("index_types_marketGroupID", nameof(MarketGroupID), OrderByType.Asc)]
    public class Types
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public int GroupID { get; set; }
        public int MarketGroupID { get; set; }
        public double Mass { get; set; }
        public double Volume { get; set; }
        [SqlSugar.SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public int MetaGroupID { get; set; }
        public int PortionSize { get; set; }
        public int RaceID { get; set; }
        public int VariationParentTypeID { get; set; }
        public double BasePrice { get; set; }
        public int IconID { get; set; }
        public bool Published { get; set; }
        public Types() { }
        public Types(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var types = model as DeserializeModels.Types;
            Id = types.Id;
            Name = types.Names?.GetValue(language);
            GroupID = types.GroupID;
            MarketGroupID = types.MarketGroupID;
            Mass = types.Mass;
            Volume = types.Volume;
            Description = types.Descriptions?.GetValue(language);
            MetaGroupID = types.MetaGroupID;
            PortionSize = types.PortionSize;
            RaceID = types.RaceID;
            VariationParentTypeID = types.VariationParentTypeID;
            BasePrice = types.BasePrice;
            IconID = types.IconID;
            Published = types.Published;
        }
    }
}
