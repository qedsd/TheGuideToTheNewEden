using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.DevTools.Map
{
    internal static class RegionMap
    {
        //public class RegionInfo
        //{
        //    public int RegionId { get; set; }
        //    public double X { get; set; }
        //    public double Y { get; set; }
        //    public List<int> JumpTo { get; set; }
        //}
        internal static void CreateMap(string solarSystemMapPath, string outPath)
        {
            var text = File.ReadAllText(solarSystemMapPath);
            var pos = JsonConvert.DeserializeObject<List<SolarSystemPosition>>(text);
            var posDic = pos.ToDictionary(p => p.SolarSystemID);
            var systemInfoDic = Core.Services.DB.MapSolarSystemService.QueryAll().ToDictionary(p => p.SolarSystemID);
            var systemInfoGroupByRegion = Core.Services.DB.MapSolarSystemService.QueryAll().GroupBy(p => p.RegionID);
            var regionInfos = Core.Services.DB.MapRegionService.QueryAll();
            Dictionary<int, RegionPosition> rDic = new Dictionary<int, RegionPosition>();
            foreach(var regionInfo in regionInfos)
            {
                RegionPosition regionInfo2 = new RegionPosition()
                {
                    RegionId = regionInfo.RegionID,
                    X = Math.Round(regionInfo.X / Math.Pow(10,15),3),//缩小坐标到百位数
                    Y = Math.Round(regionInfo.Z / Math.Pow(10, 15),3),//Z才是Y
                    JumpTo = new List<int>()
                };
                var systems = (systemInfoGroupByRegion.FirstOrDefault(p => p.Key == regionInfo.RegionID))?.ToList();//超出当前星域下所有的星系
                if(systems != null && systems.Any())
                {
                    foreach(var system in systems)//挨个找出当前星域下每一个星系连接到的星系
                    {
                        if(posDic.TryGetValue(system.SolarSystemID, out var systemInfo))
                        {
                            if (systemInfo.JumpTo != null)
                            {
                                foreach (var jumpTo in systemInfo.JumpTo)
                                {
                                    if (systemInfoDic[jumpTo].RegionID != regionInfo.RegionID)//连接的星系不在当前星域
                                    {
                                        if (!regionInfo2.JumpTo.Contains(systemInfoDic[jumpTo].RegionID))
                                        {
                                            regionInfo2.JumpTo.Add(systemInfoDic[jumpTo].RegionID);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if(regionInfo2.JumpTo.Any())
                    rDic.Add(regionInfo.RegionID, regionInfo2);
            }
            var maxX = rDic.Values.Max(p=>p.X);
            var minX = rDic.Values.Min(p => p.X);
            var maxY = rDic.Values.Max(p => p.Y);
            var minY = rDic.Values.Min(p => p.Y);
            //将xy平移到0
            var xOffset = 0 - minX;
            var yOffset = 0 - minY;
            var afterOffsetMaxY = maxY + yOffset;
            foreach (var sys in rDic.Values)
            {
                sys.X = sys.X + xOffset;
                sys.Y = afterOffsetMaxY - (sys.Y + yOffset);//将Y从向上递增改为向下递增从而符合window绘制
            }
            ////将xy缩放到最大范围内
            //double maxYUnit = 100;
            //double maxXUnit = 100;
            //var xScale = maxXUnit / (maxX + xOffset);
            //var yScale = maxYUnit / (maxY+ yOffset);
            //foreach(var sys in rDic.Values)
            //{
            //    sys.X = (sys.X + xOffset) * xScale;
            //    sys.Y = (sys.Y + yOffset) * yScale;
            //}
            var outText = JsonConvert.SerializeObject(rDic.Values);
            File.WriteAllText(outPath, outText);
        }
    }
}
