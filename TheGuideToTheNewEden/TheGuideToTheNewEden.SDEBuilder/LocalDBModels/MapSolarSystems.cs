using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SqlSugar.SugarTable("mapSolarSystems")]
    [SugarIndex("index_mapSolarSystems_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_mapSolarSystems_name", nameof(Name), OrderByType.Asc)]
    public class MapSolarSystems
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string Name { get; set; }

        public MapSolarSystems() { }
        public MapSolarSystems(DBModels.MapSolarSystems model)
        {
            Id = model.Id;
            Name = model.Name;
        }
    }
}
