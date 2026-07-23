using System;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class CharacterPlanet
    {
        public DateTime LastUpdate { get; set; }
        public long NumPins { get; set; }
        public long OwnerId { get; set; }
        public long PlanetId { get; set; }
        public string PlanetType { get; set; }
        public long SolarSystemId { get; set; }
        public long UpgradeLevel { get; set; }
        public string SolarSystemName { get; set; }
        public string PlanetName { get; set; }
        public string PlanetTypeName { get; set; }
        public string PlanetTypeEmoji { get; set; }
        public int NumExtractors { get; set; }
        public int NumFactories { get; set; }
        public int NumHeads { get; set; }
        public int NumActiveExtractors { get; set; }
        public string ProductionSummary { get; set; }
        public int AgeDays => (DateTime.UtcNow - LastUpdate).Days;
        public bool IsActive => AgeDays < 7;
        public string RuntimeDisplay => AgeDays > 30 ? (AgeDays / 30) + "个月" : AgeDays + "天";
    }
}
