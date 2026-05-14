using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("marketGroups")]
    [SugarIndex("index_marketGroups_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_marketGroups_name", nameof(Name), OrderByType.Asc)]
    public class MarketGroups
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SqlSugar.SugarColumn(IsNullable = true)]
        public string Description { get; set; }
        public MarketGroups() { }
        public MarketGroups(DBModels.MarketGroups model)
        {
            Id = model.Id;
            Name = model.Name;
            Description = model.Description;
        }
    }
}
