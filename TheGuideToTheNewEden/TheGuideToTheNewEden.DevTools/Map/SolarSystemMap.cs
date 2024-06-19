using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.DevTools.Map
{
    internal static class SolarSystemMap
    {
        internal static void CreateSloarSystemMap(string outPath)
        {
            var solarSystems = GetBase();
            if(solarSystems != null)
            {
                SetPositon(solarSystems);
                var json = JsonConvert.SerializeObject(solarSystems);
                System.IO.File.WriteAllText(outPath, json);
            }
        }
        static List<SolarSystemPosition> GetBase()
        {
            var jumps = MapSolarSystemJumpService.QueryAll();
            if (jumps != null)
            {
                List<SolarSystemPosition> solarSystemPositions = new List<SolarSystemPosition>();
                try
                {
                    var groupByFromSolarSystemID = jumps.GroupBy(p => p.FromSolarSystemID);
                    foreach (var item in groupByFromSolarSystemID)
                    {
                        SolarSystemPosition solarSystemPosition = new SolarSystemPosition();
                        solarSystemPosition.SolarSystemID = item.Key;
                        solarSystemPosition.JumpTo = item.Select(p => p.ToSolarSystemID).ToList();
                        solarSystemPositions.Add(solarSystemPosition);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                return solarSystemPositions;
            }
            else
            {
                return null;
            }
        }

        static void SetPositon(List<SolarSystemPosition> solarSystems)
        {
            var items = MapSolarSystemService.QueryAll();
            if (items != null)
            {
                items = items.Where(p => p.SolarSystemID < 31000000).ToList();//ID大于31000000的为虫洞空间之类的
            }
            var dic = items.ToDictionary(p => p.SolarSystemID);
            foreach (var item in solarSystems)
            {
                if (dic.TryGetValue(item.SolarSystemID, out var mapSolarSystem))
                {
                    item.X = Math.Round(mapSolarSystem.X / Math.Pow(10, 15), 3);
                    item.Y = Math.Round(mapSolarSystem.Z / Math.Pow(10, 15), 3);//Z才是Y
                }
            }
            var maxX = solarSystems.Max(p => p.X);
            var minX = solarSystems.Min(p => p.X);
            var maxY = solarSystems.Max(p => p.Y);
            var minY = solarSystems.Min(p => p.Y);
            //将xy平移到0
            var xOffset = 0 - minX;
            var yOffset = 0 - minY;
            var afterOffsetMaxY = maxY + yOffset;
            foreach (var sys in solarSystems)
            {
                sys.X = sys.X + xOffset;
                sys.Y = afterOffsetMaxY - (sys.Y + yOffset);//将Y从向上递增改为向下递增从而符合window绘制
            }
            ////将xy缩放到最大范围内
            //double maxYUnit = 100;
            //double maxXUnit = 100;
            //var xScale = maxXUnit / (maxX + xOffset);
            //var yScale = maxYUnit / (maxY + yOffset);
            //foreach (var sys in solarSystems)
            //{
            //    sys.X = (sys.X + xOffset) * xScale;
            //    sys.Y = maxYUnit - (sys.Y + yOffset) * yScale;
            //}  
        }
    }
}
