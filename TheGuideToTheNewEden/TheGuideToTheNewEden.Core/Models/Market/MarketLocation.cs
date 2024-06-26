﻿using System;
using System.Collections.Generic;
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
        }
        public MarketLocation(MapRegion mapRegion)
        {
            Type = MarketLocationType.Region;
            Id = mapRegion.RegionID;
            MarketObj = mapRegion;
            Name = mapRegion.RegionName;
            RegionId = mapRegion.RegionID;
        }
        public MarketLocation(Structure structure)
        {
            Type = MarketLocationType.Structure;
            Id = structure.Id;
            MarketObj = structure;
            Name = structure.Name;
            RegionId = structure.RegionId;
        }
        public MarketLocationType Type { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public object MarketObj { get; set; }
        public int RegionId { get; set; }
    }
    public enum MarketLocationType
    {
        Region, SolarSystem, Structure
    }
}
