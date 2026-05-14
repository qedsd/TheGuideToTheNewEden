using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("stations")]
    [SugarIndex("index_stations_id", nameof(Id), OrderByType.Asc)]
    [SugarIndex("index_stations_name", nameof(StationName), OrderByType.Asc)]
    public class Stations
    {
        [SugarColumn(IsPrimaryKey = true)]
        public int Id { get; set; }
        public string StationName { get; set; }
        public int SolarSystemID { get; set; }
    }
}
