using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WinUI.Models.Map
{
    public class MapData
    {
        public MapData(MapPosition pos)
        {
            OriginalX = (float)pos.X;
            OriginalY = (float)pos.Y;
        }
        public int Id { get; set; }
        public float OriginalX { get; set; }
        public float OriginalY { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public float OriginalW { get; set; } = 5;
        public float OriginalH { get; set; } = 5;
        public float W { get; set; } = 4;
        public float H { get; set; } = 4;
        public string InnerText { get; set; }
        public string MainText { get; set; }
        public Windows.UI.Color BgColor { get; set; }

        public bool Visible { get; set; } = true;

        public List<int> LinkTo;

        public float CenterX { get => X + W / 2; }
        public float CenterY { get => Y + H / 2; }

        public bool Enable { get; set; } = true;
    }
    public class MapSystemData : MapData
    {
        public MapSystemData(SolarSystemPosition solarSystemPosition, MapSolarSystem mapSolarSystem) : base(solarSystemPosition)
        {
            Id = solarSystemPosition.SolarSystemID;
            SolarSystemPosition = solarSystemPosition;
            MapSolarSystem = mapSolarSystem;
            X = (float)SolarSystemPosition.X;
            Y = (float)SolarSystemPosition.Y;
            MainText = MapSolarSystem.SolarSystemName;
            InnerText = MapSolarSystem.Security.ToString("N1");
            BgColor = Converters.SystemSecurityForegroundConverter.Convert(MapSolarSystem.Security).Color;
            LinkTo = SolarSystemPosition.JumpTo;
        }
        public SolarSystemPosition SolarSystemPosition { get; set; }
        public Core.DBModels.MapSolarSystem MapSolarSystem { get; set; }
    }
    public class MapRegionData : MapData
    {
        public RegionPosition RegionPosition { get; set; }
        public Core.DBModels.MapRegion MapRegion { get; set; }
        public MapRegionData(RegionPosition regionPosition, Core.DBModels.MapRegion mapRegion) : base(regionPosition)
        {
            Id = regionPosition.RegionId;
            RegionPosition = regionPosition;
            MapRegion = mapRegion;
            X = (float)RegionPosition.X;
            Y = (float)RegionPosition.Y;
            MainText = MapRegion.RegionName;
            BgColor = Microsoft.UI.Colors.Green;
            LinkTo = RegionPosition.JumpTo;
        }
    }
    public enum MapMode
    {
        Region, System
    }
}
