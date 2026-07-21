using System.Collections.Generic;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class PlanetRoute
    {
        public long RouteId { get; set; }
        public long SourcePinId { get; set; }
        public long DestinationPinId { get; set; }
        public int ContentTypeId { get; set; }
        public double Quantity { get; set; }
        public string ContentName { get; set; }
    }
}
