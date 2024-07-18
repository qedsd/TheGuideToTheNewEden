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
                    var json = System.IO.File.ReadAllText(Config.SolarSystemMapPath);
                    if(!string.IsNullOrEmpty(json) )
                    {
                        var list = JsonConvert.DeserializeObject<List<SolarSystemPosition>>(json);
                        if(list.NotNullOrEmpty())
                        {
                            positionDic = list.ToDictionary(p => p.SolarSystemID);
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

        public static void ResetXY(List<IntelSolarSystemMap> all)
        {
            var orderX = all.OrderBy(p => p.X).ToList();
            var orderY = all.OrderBy(p => p.Y).ToList();
            int spanCount = (int)Math.Ceiling(Math.Sqrt(all.Count));
            double spanXY = Math.Round(1f / spanCount, 2);//每档XY坐标跨越
            for (int i = 0; i < spanCount; i++)
            {
                var currentSpanItemXs = orderX.Skip(i * spanCount).Take(spanCount);
                foreach (var currentSpanItemX in currentSpanItemXs)
                {
                    currentSpanItemX.X = spanXY * i;
                }
                var currentSpanItemYs = orderY.Skip(i * spanCount).Take(spanCount);
                foreach (var currentSpanItemY in currentSpanItemYs)
                {
                    currentSpanItemY.Y = spanXY * i;
                }
            }
            //再重新按100%比例调整
            double maxX = orderX.Last().X;
            double maxY = orderY.Last().Y;
            double addX = (1 - maxX) / 2;
            double addY = (1 - maxY) / 2;
            foreach (var item in all)
            {
                item.X += addX;
                item.Y += addY;
            }

            //解决两个星系xy过于相近导致的重叠
            var sameXY = all.GroupBy(p=>new {p.X, p.Y}).ToList();
            foreach(var sames in sameXY)
            {
                int count = sames.Count();
                if (count > 1)
                {
                    var splitSpan = spanXY * 0.8 / count;
                    var moveSpan = splitSpan * (count / 2);
                    if(sames.Key.X - moveSpan < 0)//只能往右挪
                    {
                        for(int i = 0; i < count; i++)
                        {
                            sames.ElementAt(i).X += (i * moveSpan);
                        }
                    }
                    else if(sames.Key.X + moveSpan > 1)//只能往左挪
                    {
                        for (int i = 0; i < count; i++)
                        {
                            sames.ElementAt(i).X -= (i * moveSpan);
                        }
                    }
                    else//居中挪
                    {
                        for (int i = 0; i < count; i++)
                        {
                            sames.ElementAt(i).X -= moveSpan;
                            sames.ElementAt(i).X += (i * moveSpan);
                        }
                    }
                    foreach(var item in sames)
                    {
                        item.X = item.X > 1 ? 1 : item.X;
                        item.Y = item.Y > 1 ? 1 : item.Y;
                    }
                }
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
