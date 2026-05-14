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
            OriginalX = (float)pos.X2;
            OriginalY = (float)pos.Y2;
        }
        public int Id { get; set; }
        public float OriginalX { get; set; }
        public float OriginalY { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public float OriginalW { get; set; } = 4;
        public float OriginalH { get; set; } = 4;
        public float W { get; set; } = 4;
        public float H { get; set; } = 4;
        public string InnerText { get; set; }
        public string MainText { get; set; }
        public Windows.UI.Color BgColor { get; set; }

        /// <summary>
        /// 用于控制超出屏幕显示范围的点不进行绘制
        /// </summary>
        public bool Visible { get; set; } = true;

        public List<int> LinkTo;

        public float CenterX { get => X + W / 2; }
        public float CenterY { get => Y + H / 2; }

        /// <summary>
        /// 激活状态下的点才绘制颜色
        /// 否则置灰色
        /// </summary>
        public bool Active { get; set; } = true;

        public object Tag { get; set; }

        public bool Enable { get; set; } = true;

        public List<MapDataExt> DataExts { get;private set; } = new List<MapDataExt>();

        public Dictionary<int, ShipItem> Ships { get; set; } = new Dictionary<int, ShipItem>();
        public List<string> Msgs { get; set; } = new List<string>();

        public void AddDataExt(MapDataExt dataExt)
        {
            DataExts.Add(dataExt);
        }
        public void RemoveDataExt(string dataGUID)
        {
            DataExts.Remove(DataExts.FirstOrDefault(p => p.GUID == dataGUID));
        }
        public void AddShip(int shipId, int count)
        {
            if(!Ships.TryGetValue(shipId, out var shipItem))
            {
                IdName shipIdName = null;
                var type = Core.Services.DB.InvTypeService.QueryType(shipId);
                if(type == null)
                {
                    shipIdName = new IdName(shipId, shipId.ToString(), IdName.CategoryEnum.InventoryType);
                }
                else
                {
                    shipIdName = new IdName(type);
                }
                shipItem = new ShipItem() { ShipType = shipIdName, Count = 0 };
                Ships.Add(shipId, shipItem);
            }
            shipItem.Count += count;
        }
        public void AddShip(IEnumerable<int> shipIds)
        {
            foreach (var shipId in shipIds)
            {
                AddShip(shipId, 1);
            }
        }
        public void RemoveShip(int shipId, int count)
        {
            if (Ships.TryGetValue(shipId, out var shipItem))
            {
                shipItem.Count -= count;
                if (shipItem.Count <= 0)
                {
                    Ships.Remove(shipId);
                }
            }
        }
        public void AddMsg(string msg)
        {
            Msgs.Add(msg);
        }
        public void RemoveMsg()
        {
            Msgs.Clear();
        }
    }
    public class MapSystemData : MapData
    {
        public MapSystemData(SolarSystemPosition solarSystemPosition, MapSolarSystem mapSolarSystem) : base(solarSystemPosition)
        {
            Id = solarSystemPosition.SolarSystemID;
            SolarSystemPosition = solarSystemPosition;
            MapSolarSystem = mapSolarSystem;
            X = (float)SolarSystemPosition.X2;
            Y = (float)SolarSystemPosition.Y2;
            MainText = MapSolarSystem.SolarSystemName;
            InnerText = MapSolarSystem.Security.ToString("N2");
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

    public abstract class MapDataExt
    {
        public MapDataType DataType { get; set; }
        public int MapDataId {  get; set; }
        public string GUID {  get; set; }
    }

    public enum MapDataType
    {
        Ship,ZKBIntel,ChannelIntel
    }
    public class ShipItem
    {
        public IdName ShipType { get; set; }
        public int Count { get; set; }
    }
}
