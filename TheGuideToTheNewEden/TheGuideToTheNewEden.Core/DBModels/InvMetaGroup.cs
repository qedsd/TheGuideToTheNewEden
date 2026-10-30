using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("metaGroups")]
    public class InvMetaGroup
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int MetaGroupID { get; set; }

        [SugarColumn(ColumnName = "Name", IsNullable = true)]
        public string Name { get; set; }
    }
}
