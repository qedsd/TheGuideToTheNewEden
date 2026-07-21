using System;
using System.Collections.Generic;
namespace TheGuideToTheNewEden.Core.Models.PlanetColony
{
    public class PlanetPin
    {
        public long PinId { get; set; }
        public int TypeId { get; set; }
        public int? SchematicId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public ExtractorDetails Extractor { get; set; }
        public FactoryDetails Factory { get; set; }
        public long StorageQuantity { get; set; }
        public int? ContentTypeId { get; set; }
        public DateTime? LastCycleStart { get; set; }
        public string TypeName { get; set; }
        public PinCategory Category { get; set; }
        public string SchematicName { get; set; }
        public string SchematicTier { get; set; }
    }
    public class ExtractorDetails
    {
        public List<ExtractorHead> Heads { get; set; } = new List<ExtractorHead>();
        public int ProductTypeId { get; set; }
        public int CycleTime { get; set; }
        public double HeadRadius { get; set; }
        public int QtyPerCycle { get; set; }
        public DateTime InstallTime { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string ProductName { get; set; }
        public int NumHeads => Heads?.Count ?? 0;
        public TimeSpan Remaining => ExpiryTime - DateTime.UtcNow;
        public bool IsExpired => Remaining.TotalSeconds <= 0;
    }
    public class ExtractorHead
    {
        public int HeadId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    public class FactoryDetails
    {
        public int SchematicId { get; set; }
    }
    public enum PinCategory
    {
        CommandCenter, ExtractorControlUnit, BasicIndustryFacility,
        AdvancedIndustryFacility, HighTechProductionPlant, StorageFacility, Launchpad
    }
}
