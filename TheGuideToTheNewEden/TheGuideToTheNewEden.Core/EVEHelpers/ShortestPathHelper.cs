using ESI.NET.Models.Universe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class ShortestPathHelper
    {
        public static List<int> CalStargatePath(int start, int end, List<int> avoidIds, Dictionary<int, int> bridge)
        {
            Dijkstras dijkstras = new Dijkstras();
            var avoidIdsHashSet =  avoidIds.ToHashSet2();
            var ps = SolarSystemPosHelper.PositionDic;
            foreach (var p in ps.Values)
            {
                if(!avoidIdsHashSet.Contains(p.SolarSystemID))
                {
                    Dictionary<int, double> edges = new Dictionary<int, double>();
                    if (p.JumpTo.NotNullOrEmpty())//添加星门关联的星系
                    {
                        foreach (var jump in p.JumpTo)
                        {
                            if (!avoidIdsHashSet.Contains(jump))
                            {
                                edges.Add(jump, 1);
                            }
                        }
                    }
                    if(bridge.TryGetValue(p.SolarSystemID, out var bridgeTo))//添加跳桥关联的星系
                    {
                        edges.Add(bridgeTo, 1);
                    }
                    if (edges.Any())
                    {
                        dijkstras.AddVertex(p.SolarSystemID, edges);
                    }
                }
            }
            return dijkstras.CalShortestPath(start, end);
        }
        private delegate double CalWeightDelegate(SolarSystemPosition p1, SolarSystemPosition p2);
        private static double EqualWeight(SolarSystemPosition p1, SolarSystemPosition p2)
        {
            return 1;
        }
        private static double DistanceWeight(SolarSystemPosition p1, SolarSystemPosition p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2) + Math.Pow(p1.Z - p2.Z, 2));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="maxJump"></param>
        /// <param name="useGate"></param>
        /// <param name="avoidIds"></param>
        /// <param name="mode">
        /// 0 时间优先 - 权重全部相等
        /// 1 省钱优先 - 以跳跃距离为权重，星门按最大跳跃距离算
        /// </param>
        /// <returns></returns>
        public static List<int> CalCapitalJumpPath(int start, int end, double maxJump,bool useGate, List<int> avoidIds,int mode)
        {
            Dijkstras dijkstras = new Dijkstras();
            var avoidIdsHashSet = avoidIds.ToHashSet2();
            var ps = SolarSystemPosHelper.PositionDic;
            var notCapJumpSystems = Services.DB.MapSolarSystemService.QueryByMinSec(0.45).Select(p=>p.SolarSystemID).ToHashSet2();

            double maxJump2 = maxJump * 9460730472580800 / Math.Pow(10, 15);//将光年缩小到与星系位置配置文件相同单位
            double gateWeight = mode == 0 ? maxJump2 : 1;
            
            CalWeightDelegate calWeight = EqualWeight;
            switch(mode)
            {
                case 0: calWeight = EqualWeight; break;
                case 1: calWeight = DistanceWeight; break;
            }
            foreach (var p in ps.Values)
            {
                if (!avoidIdsHashSet.Contains(p.SolarSystemID))
                {
                    Dictionary<int, double> edges = new Dictionary<int, double>();
                    if (useGate && p.JumpTo.NotNullOrEmpty())//星门连接
                    {
                        foreach (var jump in p.JumpTo)
                        {
                            if (!avoidIdsHashSet.Contains(jump))
                            {
                                edges.Add(jump, maxJump2);
                            }
                        }
                    }
                    //旗舰跳连接
                    var jumpTo2 = ps.Values.Where(p2=> !notCapJumpSystems.Contains(p2.SolarSystemID) && Math.Abs(p2.X - p.X) <= maxJump2 && Math.Abs(p2.Y - p.Y) <= maxJump2 && Math.Abs(p2.Z - p.Z) <= maxJump2 && Math.Sqrt(Math.Pow(p2.X - p.X,2) + Math.Pow(p2.Y - p.Y, 2) + Math.Pow(p2.Z - p.Z, 2)) <= maxJump2).ToList();
                    foreach (var jump in jumpTo2)
                    {
                        if (jump.SolarSystemID != p.SolarSystemID && !avoidIdsHashSet.Contains(jump.SolarSystemID))
                        {
                            edges.TryAdd(jump.SolarSystemID, calWeight(p, jump));
                        }
                    }
                    dijkstras.AddVertex(p.SolarSystemID, edges);
                }
            }
            return dijkstras.CalShortestPath(start, end);
        }

        public static List<MapSolarSystem> CalOneJumpCover(int systemId, double maxJump)
        {
            double maxJump2 = maxJump * 9460730472580800 / Math.Pow(10, 15);//将光年缩小到与星系位置配置文件相同单位
            var ps = SolarSystemPosHelper.PositionDic;
            var centerSystem = ps[systemId];
            var coversPos = ps.Values.Where(p => Math.Abs(centerSystem.X - p.X) <= maxJump2 && Math.Abs(centerSystem.Y - p.Y) <= maxJump2 && Math.Abs(centerSystem.Z - p.Z) <= maxJump2 && Math.Sqrt(Math.Pow(centerSystem.X - p.X, 2) + Math.Pow(centerSystem.Y - p.Y, 2) + Math.Pow(centerSystem.Z - p.Z, 2)) <= maxJump2).ToList();
            if(coversPos.NotNullOrEmpty())
            {
                return Services.DB.MapSolarSystemService.Query(coversPos.Select(p => p.SolarSystemID).ToList()).Where(p => p.Security < 0.45).ToList();
            }
            else
            {
                return null;
            }
        }
    }
}
