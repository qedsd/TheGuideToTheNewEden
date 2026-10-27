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
        /// **不删除任何星系**：以 2%–98% 分位作为核心视口，超出分位的尾部映射到外侧 4% 边带做软压缩，
        /// 保留方向和先后顺序；被标为 <see cref="SolarSystemPosition.ViewportOutlier"/> 的只是“几何离群”，
        /// 数据仍属于当前 N 跳范围，由界面决定是否贴边显示。
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

            var p2x = Percentile(xs, 0.02);
            var p98x = Percentile(xs, 0.98);
            var p2y = Percentile(ys, 0.02);
            var p98y = Percentile(ys, 0.98);
            if (p98x - p2x < 1e-9)
            {
                p2x = minX;
                p98x = maxX;
            }
            if (p98y - p2y < 1e-9)
            {
                p2y = minY;
                p98y = maxY;
            }

            var rangeX = p98x - p2x;
            var rangeY = p98y - p2y;
            // 留出 5% 外边距；纵横比仍保持一致。
            var scaleRatio = Math.Min(0.90 / rangeX, 0.90 / rangeY);
            var offsetX = (1 - rangeX * scaleRatio) / 2;
            var offsetY = (1 - rangeY * scaleRatio) / 2;

            foreach (var point in all)
            {
                var rawX = point.X2;
                var rawY = point.Y2;
                point.ViewportOutlier =
                    rawX < p2x - 1e-9 || rawX > p98x + 1e-9 ||
                    rawY < p2y - 1e-9 || rawY > p98y + 1e-9;

                point.X2 = MapAxis(rawX, minX, p2x, p98x, maxX, scaleRatio, offsetX);
                point.Y2 = MapAxis(rawY, minY, p2y, p98y, maxY, scaleRatio, offsetY);
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
        /// 单轴映射：核心段按比例展开；分位之外落在外侧 4% 边带（软压缩），而不是全部贴成一条边。
        /// </summary>
        private static double MapAxis(
            double value,
            double min,
            double p2,
            double p98,
            double max,
            double scale,
            double offset)
        {
            if (value < p2)
            {
                var span = p2 - min;
                if (span < 1e-9)
                {
                    return Math.Max(0.005, offset);
                }

                return Math.Max(0.005, offset - 0.04 * ((p2 - value) / span));
            }

            if (value > p98)
            {
                var span = max - p98;
                var coreEnd = offset + (p98 - p2) * scale;
                if (span < 1e-9)
                {
                    return Math.Min(0.995, coreEnd);
                }

                return Math.Min(0.995, coreEnd + 0.04 * ((value - p98) / span));
            }

            return offset + (value - p2) * scale;
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
