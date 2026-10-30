namespace TheGuideToTheNewEden.Core.Models.KB
{
    /// <summary>被击杀舰船的 SDE 分类（用于舰船分类过滤）。</summary>
    public sealed class ShipTypeClass
    {
        public int TypeID { get; set; }
        public string Name { get; set; }
        public int GroupID { get; set; }
        public int MetaGroupID { get; set; }
    }
}
