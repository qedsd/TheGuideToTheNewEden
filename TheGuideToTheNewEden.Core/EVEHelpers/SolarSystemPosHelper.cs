using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Extensions;
using System.Linq;
using TheGuideToTheNewEden.Core.Services.DB;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class SolarSystemPosHelper
    {
        private static Dictionary<int, SolarSystemPosition> positionDic;
        private static Dictionary<int, SolarSystemPosition> PositionDic
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
                            if(position.Jumps.NotNullOrEmpty())
                            {
                                continue;//说明前面已经找过此星系的
                            }
                            if (position.Jumps == null)
                            {
                                position.Jumps = new List<IntelSolarSystemMap>();
                            }
                            foreach (var jumpToId in position.JumpTo)
                            {
                                if (PositionDic.TryGetValue(jumpToId, out var jumpTo))
                                {
                                    var next = new IntelSolarSystemMap(jumpTo);
                                    newJumpList.Add(next);//将一跳外星系加入下一跳数的星系列表中
                                    position.Jumps.Add(next);
                                }
                            }
                        }
                    }
                    //将当前跳数星系所有的一跳外星系去重加入结果列表
                    if (newJumpList.Any())
                    {
                        var distinct = newJumpList.Distinct();
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

        public static void ResetXY(List<IntelSolarSystemMap> all,int refP = 10)
        {
            var maxX = all.Max(p => p.X);
            var minX = all.Min(p => p.X);
            var maxY = all.Max(p=>p.Y);
            var minY = all.Min(p => p.Y);
            var xSpan = maxX - minX;
            var ySpan = maxY - minY;
            double percent = refP / 100;
            foreach (var position in all)
            {
                var beforeXPercent = (position.X - minX) / xSpan;//x在所有点x原始范围内比例
                var beforeYPercent = (position.Y - minY) / ySpan;//y在所有点y原始范围内比例
                //var afterXpercent = 
                position.X = beforeXPercent;
                position.Y = beforeYPercent;
            }
        }
    }
}
