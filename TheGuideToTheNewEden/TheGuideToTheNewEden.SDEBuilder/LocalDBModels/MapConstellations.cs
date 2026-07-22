using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("mapConstellations")]
    [SugarIndex("index_mapConstellations_id", nameof(Id), OrderByType.Asc)]
    public class MapConstellations
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        public MapConstellations() { }
        public MapConstellations(DBModels.MapConstellations model)
        {
            Id = model.Id;
            Name = model.Name;
        }
    }
}
