using System.Collections.Generic;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>行星开发 schematic：每周期消耗 inputs，产出 OutputQuantity 个产物。</summary>
    public sealed class IndustryPlanetaryRecipe
    {
        public int SchematicId { get; set; }
        public int ProductTypeID { get; set; }
        public string Name { get; set; }
        public int CycleTime { get; set; }
        public long OutputQuantity { get; set; }
        public List<IndustryPlanetaryInput> Inputs { get; set; } = new List<IndustryPlanetaryInput>();
    }

    public sealed class IndustryPlanetaryInput
    {
        public int TypeID { get; set; }
        public long Quantity { get; set; }
    }
}
