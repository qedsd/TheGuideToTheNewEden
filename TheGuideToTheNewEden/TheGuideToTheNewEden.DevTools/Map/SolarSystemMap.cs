using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            if(items != null)
            {
                items = items.Where(p => p.SolarSystemID < 31000000).ToList();//ID大于31000000的为虫洞空间之类的
                int referenceX = 1000;//最终重新计算的x位置最大值
                int referenceY = 1000;//最终重新计算的y位置最大值
                var maxX = items.Max(p => p.X);
                var minX = items.Min(p => p.X);
                var maxY = items.Max(p => p.Y);
                var minY = items.Min(p => p.Y);
                var xSpan = maxX - minX;//整个X区间大小
                var ySpan = maxY - minY;//整个Y区间大小
                //要计算某个x在当前区间内对应百分比位置，就此x与所有x中最小x的差，除以x区间即可，y同理
                var dic = items.ToDictionary(p => p.SolarSystemID);
                foreach (var item in solarSystems)
                {
                    if(dic.TryGetValue(item.SolarSystemID, out var mapSolarSystem))
                    {
                        double xP = (mapSolarSystem.X - minX) / xSpan;
                        double yP = (mapSolarSystem.Y - minY) / ySpan;
                        //item.X = xP * referenceX;//从0到referenceX重新赋值
                        //item.Y = yP * referenceY;//从0到referenceY重新赋值
                        item.X = xP;
                        item.Y = yP;
                    }
                }
            }
        }
    }
}
