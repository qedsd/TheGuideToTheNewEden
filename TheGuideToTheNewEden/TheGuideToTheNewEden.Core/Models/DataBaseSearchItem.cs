using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models
{
    public class DataBaseSearchItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsLocal { get; set; }
        public DataBaseItemType Type {  get; set; }
        public DataBaseSearchItem()
        {

        }
        public DataBaseSearchItem(InvType invType)
        {
            ID = invType.TypeID;
            Name = invType.TypeName;
            Description = invType.Description;
            Type = DataBaseItemType.InvType;
        }
        public DataBaseSearchItem(InvTypeBase invType, bool isLocal = true)
        {
            ID = invType.TypeID;
            Name = invType.TypeName;
            Description = invType.Description;
            IsLocal = isLocal;
            Type = DataBaseItemType.InvType;
        }

        public DataBaseSearchItem(MapRegion mapRegion)
        {
            ID = mapRegion.RegionID;
            Name = mapRegion.RegionName;
            Description = string.Empty;
            Type = DataBaseItemType.MapRegion;
        }
        public DataBaseSearchItem(MapRegionBase mapRegion, bool isLocal = true)
        {
            ID = mapRegion.RegionID;
            Name = mapRegion.RegionName;
            Description = string.Empty;
            Type = DataBaseItemType.MapRegion;
            IsLocal = isLocal;
        }

        public DataBaseSearchItem(MapSolarSystem mapSolarSystem)
        {
            ID = mapSolarSystem.SolarSystemID;
            Name = mapSolarSystem.SolarSystemName;
            Type = DataBaseItemType.MapSolarSystem;
        }
        public DataBaseSearchItem(MapSolarSystemBase mapSolarSystem, bool isLocal = true)
        {
            ID = mapSolarSystem.SolarSystemID;
            Name = mapSolarSystem.SolarSystemName;
            Type = DataBaseItemType.MapSolarSystem;
            IsLocal = isLocal;
        }

        public DataBaseSearchItem(StaStation station)
        {
            ID = station.StationID;
            Name = station.StationName;
            Type = DataBaseItemType.StaStation;
        }
        public DataBaseSearchItem(StaStationBase station, bool isLocal = true)
        {
            ID = station.StationID;
            Name = station.StationName;
            Type = DataBaseItemType.StaStation;
            IsLocal = isLocal;
        }
        public DataBaseSearchItem(InvGroup group)
        {
            ID = group.GroupID;
            Name = group.GroupName;
            Type = DataBaseItemType.InvGroup;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
