using System.Collections.Generic;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class PlanetColonyDetail
    {
        public long PlanetId { get; set; }
        public long OwnerId { get; set; }
        public long UpgradeLevel { get; set; }
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
