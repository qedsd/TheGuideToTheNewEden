using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using static System.Net.Mime.MediaTypeNames;

namespace TheGuideToTheNewEden.Core.Models.Map
{
    public class IntelSolarSystemMap: SolarSystemPosition
    {
        public IntelSolarSystemMap() { }
        public IntelSolarSystemMap(SolarSystemPosition pos)
        {
            this.CopyFrom(pos);
        }
        public List<IntelSolarSystemMap> Jumps { get; set; }

        public bool Contain(int solaySystemId)
        {
            if (JumpsOf(solaySystemId) == -1)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public int JumpsOf(int solaySystemId)
        {
            if(SolarSystemID == solaySystemId)
            {
                return 0;
            }
            else
            {
                if (Jumps.NotNullOrEmpty())
                {
                    foreach (var jump in Jumps)
                    {
                        int nextIndex = jump.JumpsOf(solaySystemId);
                        if(nextIndex != -1)
                        {
                            return nextIndex + 1;
                        }
                    }
                }
                return -1;
            }
        }

        public List<IntelSolarSystemMap> GetAllSolarSystem()
        {
            //List<IntelSolarSystemMap> list = new List<IntelSolarSystemMap>() { this };
            //if (Jumps.NotNullOrEmpty())
            //{
            //    foreach (var item in Jumps)
            //    {
            //        var next = item.GetAllSolarSystem();
            //        if (next.NotNullOrEmpty())
            //        {
            //            list.AddRange(next);
            //        }
            //    }
            //    return list.GroupBy(p=>p.SolarSystemID).Select(p=>p.First()).ToList();
            //}
            //else
            //{
            //    return list;
            //}

            Dictionary<int, IntelSolarSystemMap> foundMaps = new Dictionary<int, IntelSolarSystemMap>
            {
                { this.SolarSystemID, this }
            };
            List<IntelSolarSystemMap> currentJumpList = new List<IntelSolarSystemMap>()
            {
                this
            };//当前跳数的星系，开始只有中心星系一个
            List<IntelSolarSystemMap> newJumpList = new List<IntelSolarSystemMap>();//下一跳数的星系
            while(currentJumpList.Count > 0)
            {
                foreach(var jump in currentJumpList)
                {
                    if (jump.Jumps.NotNullOrEmpty())
                    {
                        foreach (var nextJump in jump.Jumps)
                        {
                            if (!foundMaps.ContainsKey(nextJump.SolarSystemID))
                            {
                                foundMaps.Add(nextJump.SolarSystemID, nextJump);
                                newJumpList.Add(nextJump);//将一跳外星系加入下一跳数的星系列表中
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
            return foundMaps.Values.ToList();
        }
    }
}
