using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("stations")]
    public class StaStation : StaStationBase
    {
        public int SolarSystemID { get; set; }
    }

    [SugarTable("stations")]
    public class StaStationBase
    {
        [Display(Order = 1)]
        [SugarColumn(ColumnName = "Id")]
        public int StationID { get; set; }

        [Display(Order = 2)]
        public string StationName { get; set; }
    }
}
