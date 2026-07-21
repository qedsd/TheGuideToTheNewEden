using System;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class CharacterPlanet
    {
        public int PlanetId { get; set; }
        public int OwnerId { get; set; }
        public int SolarSystemId { get; set; }
        public string PlanetType { get; set; }
        public int UpgradeLevel { get; set; }
        public int NumPins { get; set; }
        public DateTime LastUpdate { get; set; }
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
        public static string GetPlanetTypeEmoji(string pt)
        {
            switch (pt?.ToLowerInvariant())
            { case "temperate": return "\U0001f33f"; case "barren": return "\U0001f3dc"; case "gas": return "\U0001f4a8"; case "ice": return "\u2744"; case "lava": return "\U0001f30b"; case "oceanic": return "\U0001f30a"; case "plasma": return "\u26a1"; case "storm": return "\U0001f300"; default: return "\U0001fa90"; }
        }
    }
}
