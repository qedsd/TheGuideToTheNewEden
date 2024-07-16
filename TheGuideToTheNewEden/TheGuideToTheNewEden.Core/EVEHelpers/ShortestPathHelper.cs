using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class ShortestPathHelper
    {
        public static List<int> CalStargatePath(int start, int end, List<int> avoidIds)
        {
            Dijkstras dijkstras = new Dijkstras();
            var avoidIdsHashSet =  avoidIds.ToHashSet2();
            var ps = SolarSystemPosHelper.PositionDic;
            foreach (var p in ps.Values)
            {
                if(!avoidIdsHashSet.Contains(p.SolarSystemID))
                {
                    if (p.JumpTo.NotNullOrEmpty())
                    {
                        Dictionary<int, double> edges = new Dictionary<int, double>();
                        foreach (var jump in p.JumpTo)
                        {
                            if (!avoidIdsHashSet.Contains(jump))
                            {
                                edges.Add(jump, 1);
                            }
                        }
                        dijkstras.AddVertex(p.SolarSystemID, edges);
                    }
                }
            }
            return dijkstras.CalShortestPath(start, end);
        }
        public static List<int> CalCapitalJumpPath(int start, int end, double maxJump,bool useGate, List<int> avoidIds)
        {
            Dijkstras dijkstras = new Dijkstras();
            var avoidIdsHashSet = avoidIds.ToHashSet2();
            var ps = SolarSystemPosHelper.PositionDic;
            var notCapJumpSystems = Services.DB.MapSolarSystemService.QueryByMinSec(0.5).Select(p=>p.SolarSystemID).ToHashSet2();
            //SolarSystemPosition kmq4 = new SolarSystemPosition()
            //{
            //    SolarSystemID = 30004375,
            //    X = 415.63,
            //    Y = 21.855000000000018,
            //    JumpTo = new List<int>() { 30004374 }
            //};
            //SolarSystemPosition p5 = new SolarSystemPosition()
            //{
            //    SolarSystemID = 30004374,
            //    X = 400.658,
            //    Y = 24.979000000000042,
            //    JumpTo = new List<int>() { 30004373, 30004375 }
            //};
            //SolarSystemPosition j52 = new SolarSystemPosition()
            //{
            //    SolarSystemID = 30004373,
            //    X = 399.56300000000005,
            //    Y = 33.50800000000004,
            //    JumpTo = new List<int>() { 30004374, 30004372 }
            //};
            //SolarSystemPosition clb = new SolarSystemPosition()
            //{
            //    SolarSystemID = 30004372,
            //    X = 401.249,
            //    Y = 36.298,
            //    JumpTo = new List<int>() { 30004373 }
            //};
            //Dictionary<int, SolarSystemPosition> ps = new Dictionary<int, SolarSystemPosition>()
            //{
            //    {kmq4.SolarSystemID, kmq4 },
            //    {p5.SolarSystemID, p5 },
            //    {j52.SolarSystemID, j52 },
            //    {clb.SolarSystemID, clb },
            //};

            double maxJump2 = maxJump * 9460730472580800 / Math.Pow(10, 15);//将光年缩小到与星系位置配置文件相同单位
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
                            edges.TryAdd(jump.SolarSystemID, Math.Sqrt(Math.Pow(p.X - jump.X, 2) + Math.Pow(p.Y - jump.Y, 2) + Math.Pow(p.Z - jump.Z, 2)));
                        }
                    }
                    dijkstras.AddVertex(p.SolarSystemID, edges);
                }
            }
            return dijkstras.CalShortestPath(start, end);
        }
    }
}
