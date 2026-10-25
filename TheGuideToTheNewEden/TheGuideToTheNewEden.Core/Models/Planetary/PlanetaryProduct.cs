using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.Core.Models.Planetary
{
    public sealed class PlanetaryProduct
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public PlanetaryProductTier Tier { get; set; }
        public double Volume { get; set; }
        public IndustryPlanetaryRecipe Recipe { get; set; }

        public bool IsRaw => Tier == PlanetaryProductTier.P0;
        public bool IsProduced => Recipe != null;
    }
}
