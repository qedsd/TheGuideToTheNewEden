using System;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class PlanetAlert
    {
        public string CharacterName { get; set; }
        public string PlanetName { get; set; }
        public long PlanetId { get; set; }
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
        public string ResourceName { get; set; }
        public DateTime? ExpiryTime { get; set; }
    }
    public enum AlertType { ExtractorExpiring, ExtractorExpired, StorageFull, ChainBroken }
    public enum AlertSeverity { Info, Warning, Critical }
}
