using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Extensions;
using System.Linq;
using TheGuideToTheNewEden.Core.Services.DB;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class SolarSystemPosHelper
    {
        private static Dictionary<int, SolarSystemPosition> positionDic;
        public static Dictionary<int, SolarSystemPosition> PositionDic
        {
            get
            {
                if (positionDic == null)
                {
                    var allJumpsDict = Core.Services.DB.MapSolarSystemJumpService.QueryAll().GroupBy(p => p.FromSolarSystemID).ToDictionary(p => p.Key);
                    var mapSolarSystems = Core.Services.DB.MapSolarSystemService.QueryAll().Where(p => !p.IsSpecial()).ToDictionary(p => p.SolarSystemID);
                    positionDic = new Dictionary<int, SolarSystemPosition>();
                    var maxY = mapSolarSystems.Max(p => p.Value.Y2);
                    foreach (var mapSolarSystem in mapSolarSystems.Values)
                    {
                        if (allJumpsDict.TryGetValue(mapSolarSystem.SolarSystemID, out var jumps))
                        {
                            SolarSystemPosition pos = new SolarSystemPosition()
                            {
                                X = mapSolarSystem.X,
                                Y = mapSolarSystem.Y,
                                Z = mapSolarSystem.Z,
                                X2 = mapSolarSystem.X2,
                                Y2 = maxY - mapSolarSystem.Y2,//游戏原点在左下角，软件原点在左上角，需要将Y轴翻转
                                SolarSystemID = mapSolarSystem.SolarSystemID,
                                SolarSystemName = mapSolarSystem.SolarSystemName,
                                JumpTo = jumps.Select(p => p.ToSolarSystemID).ToList(),
                            };
                            positionDic.Add(pos.SolarSystemID, pos);
                        }
                    }
                }
                return positionDic;
            }
        }
        public static async Task<IntelSolarSystemMap> GetIntelSolarSystemMapAsync(int centerId, int jumps)
        {
            return await Task.Run(() =>
            {
                return GetIntelSolarSystemMap(centerId, jumps);
            });
        }
        public static IntelSolarSystemMap GetIntelSolarSystemMap(int centerId, int jumps)
        {
            if (PositionDic.TryGetValue(centerId, out var center))
            {
                var map = GetBaseIntelSolarSystemMap(centerId, jumps);
                if (map != null)
                {
                    var all = map.GetAllSolarSystem();
                    var names = MapSolarSystemService.Query(all.Select(p => p.SolarSystemID).ToList());
                    if (names.NotNullOrEmpty())
                    {
                        foreach (var system in all)
                        {
                            var name = names.FirstOrDefault(p => p.SolarSystemID == system.SolarSystemID);
                            if (name != null)
                            {
                                system.SolarSystemName = name.SolarSystemName;
                            }
                        }
                    }
                }
                GC.Collect();
                return map;
            }
            return null;
        }
        private static List<IntelSolarSystemMap> GetJumpTo(List<int> jumpToIds, List<SolarSystemPosition> all)
        {
            if(jumpToIds.NotNullOrEmpty())
            {
                List<IntelSolarSystemMap> maps = new List<IntelSolarSystemMap>();
                foreach(var jumpToId in jumpToIds)
                {
                    var pos = all.FirstOrDefault(p => p.SolarSystemID == jumpToId);
                    if(pos != null)
                    {
                        var map = new IntelSolarSystemMap()
                        {
                            SolarSystemID = pos.SolarSystemID,
                            X = pos.X,
                            Y = pos.Y,
                            JumpTo = pos.JumpTo
                        };
                        //map.CopyFrom(pos);
                        //var map = pos.DepthClone<IntelSolarSystemMap>();
                        if(map.JumpTo.NotNullOrEmpty())
                        {
                            var jumpTo = map.JumpTo.ToList();
                            var needRemoved = map.JumpTo.Intersect(jumpToIds).ToList();
                            if(needRemoved.NotNullOrEmpty())
                            {
                                foreach(var item in needRemoved)
                                {
                                    jumpTo.Remove(item);
                                }
                            }
                            if(jumpTo.NotNullOrEmpty())
                            {
                                map.Jumps = GetJumpTo(jumpTo, all);
                                maps.Add(map);
                            }
                        }
                        
                    }
                }
                return maps;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 获取星系指定跳数外的所有星系
        /// </summary>
        /// <param name="centerId"></param>
        /// <param name="jumps"></param>
        /// <returns></returns>
        public static List<SolarSystemPosition> GetSolarSystemOnJumps(int centerId, int jumps)
        {
            if (PositionDic.TryGetValue(centerId, out var center))
            {
                List<SolarSystemPosition> all = new List<SolarSystemPosition>();
                List<SolarSystemPosition> currentJumpList = new List<SolarSystemPosition>()
                {
                    center
                };//当前跳数的星系，开始只有中心星系一个
                List<SolarSystemPosition> newJumpList = new List<SolarSystemPosition>();//下一跳数的星系
                for (int i=0; i< jumps; i++)
                {
                    foreach(var position in currentJumpList)//找出当前跳数星系所有的一跳外星系
                    {
                        if (position.JumpTo.NotNullOrEmpty())
                        {
                            foreach (var jumpToId in position.JumpTo)
                            {
                                if (PositionDic.TryGetValue(jumpToId, out var jumpTo))
                                {
                                    newJumpList.Add(jumpTo);//将一跳外星系加入下一跳数的星系列表中

                                }
                            }
                        }
                    }
                    //将当前跳数星系所有的一跳外星系去重加入结果列表
                    if(newJumpList.Any())
                    {
                        var distinct = newJumpList.Distinct();
                        all.AddRange(distinct);
                        currentJumpList.Clear();
                        currentJumpList.AddRange(newJumpList);
                    }
                    else
                    {
                        break;
                    }
                }
                return all;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 获取星系指定跳数外的所有星系
        /// </summary>
        /// <param name="centerId"></param>
        /// <param name="jumps"></param>
        /// <returns></returns>
        private static IntelSolarSystemMap GetBaseIntelSolarSystemMap(int centerId, int jumps)
        {
            if (PositionDic.TryGetValue(centerId, out var center))
            {
                IntelSolarSystemMap map = new IntelSolarSystemMap();
                map.CopyFrom(center);
                Dictionary<int, IntelSolarSystemMap> foundMaps = new Dictionary<int, IntelSolarSystemMap>();
                foundMaps.Add(centerId, map);
                List<IntelSolarSystemMap> currentJumpList = new List<IntelSolarSystemMap>()
                {
                    map
                };//当前跳数的星系，开始只有中心星系一个
                List<IntelSolarSystemMap> newJumpList = new List<IntelSolarSystemMap>();//下一跳数的星系
                for (int i = 0; i < jumps; i++)
                {
                    foreach (var position in currentJumpList)//找出当前跳数星系所有的一跳外星系
                    {
                        if (position.JumpTo.NotNullOrEmpty())
                        {
                            //if(position.Jumps.NotNullOrEmpty())
                            //{
                            //    continue;//说明前面已经找过此星系的
                            //}
                            if (position.Jumps == null)
                            {
                                position.Jumps = new List<IntelSolarSystemMap>();
                            }
                            foreach (var jumpToId in position.JumpTo)
                            {
                                if (PositionDic.TryGetValue(jumpToId, out var jumpTo))
                                {
                                    if(foundMaps.TryGetValue(jumpToId,out var nextMap))
                                    {
                                        position.Jumps.Add(nextMap);//直接添加下一跳星系，但不用查找下一跳星系的下一跳
                                    }
                                    else
                                    {
                                        var next = new IntelSolarSystemMap(jumpTo);
                                        newJumpList.Add(next);//将一跳外星系加入下一跳数的星系列表中
                                        position.Jumps.Add(next);
                                    }
                                }
                            }
                        }
                    }
                    //将当前跳数星系所有的一跳外星系去重加入结果列表
                    if (newJumpList.Any())
                    {
                        var distinct = newJumpList.Distinct().ToList();
                        newJumpList.Clear();
                        currentJumpList.Clear();
                        currentJumpList.AddRange(distinct);
                    }
                    else
                    {
                        break;
                    }
                }
                return map;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 将范围内星系的 2D 坐标适配到 [0,1] 视口。
        /// <para>
        /// 阶段 316：稳健核心 + 平滑压缩——核心段取 5%~95% 分位（纵横比一致），占满约 88% 视口；
        /// 超出核心的"远星系"（如吉他 4 跳外的阿巴宗）不再按全量线性缩放把核心挤成一团，
        /// 而是按 <c>sqrt</c> 缓动压进两侧各 6% 的过渡带：单调、保持次序与相对远近，
        /// 核心星系之间的相对位置几乎不受最远点影响。
        /// <see cref="SolarSystemPosition.ViewportOutlier"/> 恒为 false（312 已无贴边卡片）。
        /// </para>
        /// </summary>
        public static void ResetXY(List<IntelSolarSystemMap> all)
        {
            if (all == null || all.Count == 0)
            {
                return;
            }

            var xs = all.Select(p => p.X2).OrderBy(p => p).ToArray();
            var ys = all.Select(p => p.Y2).OrderBy(p => p).ToArray();
            var minX = xs[0];
            var maxX = xs[^1];
            var minY = ys[0];
            var maxY = ys[^1];

            // 样本太少时分位没有意义，退回全量 min/max。
            if (all.Count < 8 || maxX - minX < 1e-9 || maxY - minY < 1e-9)
            {
                ResetXYByBounds(all, minX, maxX, minY, maxY);
                foreach (var point in all)
                {
                    point.ViewportOutlier = false;
                }

                return;
            }

            var lowX = Percentile(xs, 0.05);
            var highX = Percentile(xs, 0.95);
            var lowY = Percentile(ys, 0.05);
            var highY = Percentile(ys, 0.95);
            if (highX - lowX < 1e-9)
            {
                lowX = minX;
                highX = maxX;
            }

            if (highY - lowY < 1e-9)
            {
                lowY = minY;
                highY = maxY;
            }

            var rangeX = highX - lowX;
            var rangeY = highY - lowY;
            // 核心段占满约 88% 视口；纵横比保持一致。
            var scaleRatio = Math.Min(0.88 / rangeX, 0.88 / rangeY);
            var offsetX = (1 - rangeX * scaleRatio) / 2;
            var offsetY = (1 - rangeY * scaleRatio) / 2;

            foreach (var point in all)
            {
                point.X2 = MapAxis(point.X2, minX, lowX, highX, maxX, scaleRatio, offsetX);
                point.Y2 = MapAxis(point.Y2, minY, lowY, highY, maxY, scaleRatio, offsetY);
                point.ViewportOutlier = false;
            }
        }

        /// <summary>取排序数组的分位值（线性夹取，避免越界）。</summary>
        private static double Percentile(double[] sorted, double percentile)
        {
            if (sorted.Length == 0)
            {
                return 0;
            }

            var index = (int)Math.Floor((sorted.Length - 1) * Math.Clamp(percentile, 0, 1));
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }

        /// <summary>
        /// 单轴映射：核心段 [pLow,pHigh] 线性展开到 [offset, offset+range*scale]；
        /// 分位之外按 sqrt 缓动压进 6% 过渡带（随超出比例平滑趋近边界，保持单调与相对远近）。
        /// </summary>
        private static double MapAxis(
            double value,
            double min,
            double pLow,
            double pHigh,
            double max,
            double scale,
            double offset)
        {
            const double Pad = 0.035; // 阶段 319：视口内边距——圆点半径+标签不越界（此前 0.005 会把边缘圆点裁一半）
            const double Band = 0.05;
            if (value < pLow)
            {
                var span = pLow - min;
                if (span < 1e-9)
                {
                    return Math.Max(Pad, offset);
                }

                var excess = (pLow - value) / span;
                return Math.Max(Pad, offset - Band * Math.Sqrt(excess));
            }

            if (value > pHigh)
            {
                var span = max - pHigh;
                var coreEnd = offset + (pHigh - pLow) * scale;
                if (span < 1e-9)
                {
                    return Math.Min(1 - Pad, coreEnd);
                }

                var excess = (value - pHigh) / span;
                return Math.Min(1 - Pad, coreEnd + Band * Math.Sqrt(excess));
            }

            return offset + (value - pLow) * scale;
        }

        /// <summary>全量 min/max 适配（小样本或退化范围使用）。</summary>
        private static void ResetXYByBounds(
            List<IntelSolarSystemMap> all,
            double minX,
            double maxX,
            double minY,
            double maxY)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            if (rangeX < 1e-9)
            {
                rangeX = 1;
            }
            if (rangeY < 1e-9)
            {
                rangeY = 1;
            }

            var scaleRatio = Math.Min(0.96 / rangeX, 0.96 / rangeY);
            var offsetX = (1 - rangeX * scaleRatio) / 2;
            var offsetY = (1 - rangeY * scaleRatio) / 2;
            foreach (var point in all)
            {
                point.X2 = ((point.X2 - minX) * scaleRatio) + offsetX;
                point.Y2 = ((point.Y2 - minY) * scaleRatio) + offsetY;
            }
        }
        public static int GetShortesRoute(IntelSolarSystemMap home, IntelSolarSystemMap start)
        {
            //按层数寻找
            int jump = -1;
            HashSet<int> found = new HashSet<int>();
            List<IntelSolarSystemMap> currentJumpList = new List<IntelSolarSystemMap>()
                {
                    home
                };//当前跳数的星系，开始只有中心星系一个
            List<IntelSolarSystemMap> newJumpList = new List<IntelSolarSystemMap>();//下一跳数的星系
            while(currentJumpList.Count > 0)
            {
                jump += 1;
                foreach (var currentJump in currentJumpList)
                {
                    if(currentJump.SolarSystemID == start.SolarSystemID)
                    {
                        return jump;
                    }
                    else
                    {
                        if(currentJump.Jumps.NotNullOrEmpty())
                        {
                            foreach (var next in currentJump.Jumps)
                            {
                                if(!found.Contains(next.SolarSystemID))
                                {
                                    newJumpList.Add(next);
                                    found.Add(next.SolarSystemID);
                                }
                            }
                        }
                    }
                }
                //将当前跳数星系所有的一跳外星系去重加入结果列表
                if (newJumpList.Any())
                {
                    var distinct = newJumpList.Distinct().ToList();
                    newJumpList.Clear();
                    currentJumpList.Clear();
                    currentJumpList.AddRange(distinct);
                }
                else
                {
                    break;
                }
            }
            return -1;
        }

        public static List<SolarSystemPosition> GetAllWidthName()
        {
            var names = MapSolarSystemService.Query(PositionDic.Values.Where(p=>string.IsNullOrEmpty(p.SolarSystemName)).Select(p => p.SolarSystemID).ToList());
            if (names.NotNullOrEmpty())
            {
                foreach(var name in names)
                {
                    PositionDic[name.SolarSystemID].SolarSystemName = name.SolarSystemName;
                }
            }
            return PositionDic.Values.ToList();
        }
        public static async Task<List<SolarSystemPosition>> GetAllWidthNameAsync()
        {
            return await Task.Run(() => GetAllWidthName());
        }
        public static List<SolarSystemPosition> GetAll()
        {
            return PositionDic.Values.ToList();
        }

        public static HashSet<int> GetJumpTo(int systemId)
        {
            if(PositionDic.TryGetValue(systemId, out var pos))
            {
                return pos.JumpTo?.ToHashSet2();
            }
            else
            {
                return null;
            }
        }
    }
}
