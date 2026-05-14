using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("stations")]
    [SugarIndex("index_stations_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_stations_name", nameof(StationName), OrderByType.Asc)]
    public class Stations
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string StationName { get; set; }
        public Stations() { }
        public Stations(DBModels.Stations model)
        {
            Id = model.Id;
            StationName = model.StationName;
        }
    }
}
