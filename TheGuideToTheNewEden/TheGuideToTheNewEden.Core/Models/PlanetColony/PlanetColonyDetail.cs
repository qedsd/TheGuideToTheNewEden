using System.Collections.Generic;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class PlanetColonyDetail
    {
        public int PlanetId { get; set; }
        public int OwnerId { get; set; }
        public int UpgradeLevel { get; set; }
        public List<PlanetPin> Pins { get; set; } = new List<PlanetPin>();
        public List<PlanetRoute> Routes { get; set; } = new List<PlanetRoute>();
        public List<PlanetLink> Links { get; set; } = new List<PlanetLink>();
    }
    public class PlanetLink
    {
        public long SourcePinId { get; set; }
        public long DestinationPinId { get; set; }
        public int LinkLevel { get; set; }
    }
}
