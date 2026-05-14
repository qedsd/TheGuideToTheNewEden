using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("mapRegions")]
    [SugarIndex("index_mapRegions_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_mapRegions_name", nameof(Name), OrderByType.Asc)]
    public class MapRegions
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public MapRegions() { }
        public MapRegions(DBModels.MapRegions model)
        {
            Id = model.Id;
            Name = model.Name;
        }
    }
}
