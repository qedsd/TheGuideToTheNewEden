using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("staStations")]
    public class StaStation : StaStationBase
    {
        public double Security { get; set; }
        public double DockingCostPerVolume { get; set; }
        public double MaxShipVolumeDockable { get; set; }
        public double OfficeRentalCost { get; set; }
        public int OperationID { get; set; }
        public int StationTypeID { get; set; }
        public int CorporationID { get; set; }
        public int SolarSystemID { get; set; }
        public int ConstellationID { get; set; }
        public int RegionID { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double ReprocessingEfficiency { get; set; }
        public double ReprocessingStationsTake { get; set; }
        public int ReprocessingHangarFlag { get; set; }
    }

    [SugarTable("staStations")]
    public class StaStationBase
    {
        [Display(Order = 1)]
        public int StationID { get; set; }

        [Display(Order = 2)]
        [SugarColumn(IsNullable = true)]
        public string StationName { get; set; }
    }
}
