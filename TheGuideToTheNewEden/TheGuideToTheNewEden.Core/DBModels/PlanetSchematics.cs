using SqlSugar;
namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("planetSchematics")]
    public class PlanetSchematic
    {
        [SugarColumn(ColumnName = "schematic_id", IsPrimaryKey = true)]
        public int SchematicId { get; set; }
        [SugarColumn(ColumnName = "schematic_name")]
        public string SchematicName { get; set; }
        [SugarColumn(ColumnName = "cycle_time")]
        public int CycleTime { get; set; }
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
    }
}
