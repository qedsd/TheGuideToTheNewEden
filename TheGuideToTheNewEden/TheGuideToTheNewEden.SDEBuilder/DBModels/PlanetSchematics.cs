using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("planetSchematics")]
    [SugarIndex("index_planetSchematics_id", nameof(SchematicId), OrderByType.Asc)]
    public class PlanetSchematic
    {
        [SugarColumn(IsPrimaryKey = true, ColumnName = "schematic_id")]
        public int SchematicId { get; set; }
        [SugarColumn(ColumnName = "schematic_name")]
        public string SchematicName { get; set; }
        [SugarColumn(ColumnName = "cycle_time")]
        public int CycleTime { get; set; }
        public PlanetSchematic() { }
        public PlanetSchematic(DeserializeModels.BaseModel model, LanguageEnum language)
        {
            var data = model as DeserializeModels.PlanetSchematics;
            if (data == null) return;
            SchematicId = data.Id;
            SchematicName = data.Name?.GetValue(language);
            CycleTime = data.CycleTime;
        }
    }

    [SugarTable("planetSchematicsTypeMap")]
    public class PlanetSchematicTypeMap
    {
        [SugarColumn(ColumnName = "schematic_id")]
        public int SchematicId { get; set; }
        [SugarColumn(ColumnName = "type_id")]
        public int TypeId { get; set; }
        [SugarColumn(ColumnName = "quantity")]
        public int Quantity { get; set; }
        [SugarColumn(ColumnName = "is_input")]
        public bool IsInput { get; set; }
        public PlanetSchematicTypeMap() { }
    }
}
