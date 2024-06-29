using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("planetResources")]
    public class PlanetResources
    {
        [SugarColumn(IsPrimaryKey = true)]
        public long StarID { get; set; }

        public long Power { get; set; }

        public long Workforce { get; set; }

        [SugarColumn(ColumnName = "cycle_minutes")]
        public int CycleMinutes { get; set; }

        [SugarColumn(ColumnName = "harvest_silo_max")]
        public long HarvestSiloMax { get; set; }

        [SugarColumn(ColumnName = "maturation_cycle_minutes")]
        public long MaturationCycleMinutes { get; set; }

        [SugarColumn(ColumnName = "maturation_percent")]
        public int MaturationPercent { get; set; }

        [SugarColumn(ColumnName = "mature_silo_max")]
        public double MatureSiloMax { get; set; }

        [SugarColumn(ColumnName = "reagent_harvest_amount")]
        public long ReagentHarvestAmount { get; set; }

        [SugarColumn(ColumnName = "reagent_type_id")]
        public long ReagentTypeId { get; set; }
    }
}
