using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models
{
    public class TranslationSearchItem
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsLocal { get; set; }
        public TranslationSearchType Type {  get; set; }
        public TranslationSearchItem()
        {

        }
        public TranslationSearchItem(InvType invType)
        {
            ID = invType.TypeID;
            Name = invType.TypeName;
            Description = invType.Description;
            Type = TranslationSearchType.InvType;
        }
        public TranslationSearchItem(InvTypeBase invType, bool isLocal = true)
        {
            ID = invType.TypeID;
            Name = invType.TypeName;
            Description = invType.Description;
            IsLocal = isLocal;
            Type = TranslationSearchType.InvType;
        }

        public TranslationSearchItem(MapRegion mapRegion)
        {
            ID = mapRegion.RegionID;
            Name = mapRegion.RegionName;
            Description = string.Empty;
            Type = TranslationSearchType.MapRegion;
        }
        public TranslationSearchItem(MapRegionBase mapRegion, bool isLocal = true)
        {
            ID = mapRegion.RegionID;
            Name = mapRegion.RegionName;
            Description = string.Empty;
            Type = TranslationSearchType.MapRegion;
            IsLocal = isLocal;
        }

        public TranslationSearchItem(MapSolarSystem mapSolarSystem)
        {
            ID = mapSolarSystem.SolarSystemID;
            Name = mapSolarSystem.SolarSystemName;
            Type = TranslationSearchType.MapSolarSystem;
        }
        public TranslationSearchItem(MapSolarSystemBase mapSolarSystem, bool isLocal = true)
        {
            ID = mapSolarSystem.SolarSystemID;
            Name = mapSolarSystem.SolarSystemName;
            Type = TranslationSearchType.MapSolarSystem;
            IsLocal = isLocal;
        }

        public TranslationSearchItem(StaStation station)
        {
            ID = station.StationID;
            Name = station.StationName;
            Type = TranslationSearchType.StaStation;
        }
        public TranslationSearchItem(StaStationBase station, bool isLocal = true)
        {
            ID = station.StationID;
            Name = station.StationName;
            Type = TranslationSearchType.StaStation;
            IsLocal = isLocal;
        }
        public TranslationSearchItem(InvGroup group)
        {
            ID = group.GroupID;
            Name = group.GroupName;
            Type = TranslationSearchType.InvGroup;
        }
        public enum TranslationSearchType
        {
            InvType, MapSolarSystem, MapRegion, StaStation,InvGroup
        }
    }
}
