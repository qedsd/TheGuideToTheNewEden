using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.LocalDBModels
{
    [SugarTable("planetSchematics")]
    public class PlanetSchematic
    {
        [SugarColumn(IsPrimaryKey = true, ColumnName = "schematic_id")]
        public int SchematicId { get; set; }
        [SugarColumn(ColumnName = "schematic_name")]
        public string SchematicName { get; set; }
        public PlanetSchematic() { }
        public PlanetSchematic(DBModels.PlanetSchematic model)
        {
            SchematicId = model.SchematicId;
            SchematicName = model.SchematicName;
        }
    }
}
