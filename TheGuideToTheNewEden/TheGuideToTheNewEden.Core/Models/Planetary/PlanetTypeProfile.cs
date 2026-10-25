using System.Collections.Generic;

namespace TheGuideToTheNewEden.Core.Models.Planetary
{
    /// <summary>
    /// 行星类型（Temperate/Ice/...）的固定领域数据。P0 组成和设施 TypeID 不在 SDE 的
    /// planetSchematics 中，因此作为项目自己的静态领域表维护，名称到 TypeID 在运行时解析。
    /// </summary>
    public sealed class PlanetTypeProfile
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public bool IsP4Capable { get; set; }
        public List<int> P0TypeIds { get; set; } = new List<int>();
        public PlanetFacilityTypeIds Facilities { get; set; } = new PlanetFacilityTypeIds();

        public override string ToString() => Name ?? TypeId.ToString();
    }

    public sealed class PlanetFacilityTypeIds
    {
        public int CommandCenter { get; set; }
        public int Launchpad { get; set; }
        public int BasicFactory { get; set; }
        public int AdvancedFactory { get; set; }
        public int HighTechFactory { get; set; }
        public int Storage { get; set; }
        public int Extractor { get; set; }
    }
}
