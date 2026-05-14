using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Universe;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class MarketLocation
    {
        public MarketLocation() { }
        public MarketLocation(MapSolarSystem mapSolarSystem)
        {
            Type = MarketLocationType.SolarSystem;
            Id = mapSolarSystem.SolarSystemID;
            MarketObj = mapSolarSystem;
            Name = mapSolarSystem.SolarSystemName;
            RegionId = mapSolarSystem.RegionID;
            SolarSystemId = mapSolarSystem.SolarSystemID;
        }
        public MarketLocation(MapRegion mapRegion)
        {
            Type = MarketLocationType.Region;
            Id = mapRegion.RegionID;
            MarketObj = mapRegion;
            Name = mapRegion.RegionName;
            RegionId = mapRegion.RegionID;
            SolarSystemId = Core.Services.DB.MapSolarSystemService.QueryByRegionID(mapRegion.RegionID).First().SolarSystemID;//取第一个星系
        }
        public MarketLocation(Structure structure)
        {
            Type = MarketLocationType.Structure;
            Id = structure.Id;
            MarketObj = structure;
            Name = structure.Name;
            RegionId = structure.RegionId;
            SolarSystemId = structure.SolarSystemId;
        }
        public MarketLocationType Type { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public object MarketObj { get; set; }
        public int RegionId { get; set; }
        public int SolarSystemId { get; set; }
    }
    public enum MarketLocationType
    {
        Region, SolarSystem, Structure
    }
}
