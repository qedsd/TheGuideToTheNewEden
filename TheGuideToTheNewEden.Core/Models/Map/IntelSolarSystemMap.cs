using System;
using System.Collections;
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
            //按层数寻找
            int jump = -1;
            HashSet<int> found = new HashSet<int>();
            List<IntelSolarSystemMap> currentJumpList = new List<IntelSolarSystemMap>()
                {
                    this
                };//当前跳数的星系，开始只有中心星系一个
            List<IntelSolarSystemMap> newJumpList = new List<IntelSolarSystemMap>();//下一跳数的星系
            while (currentJumpList.Count > 0)
            {
                jump += 1;
                foreach (var currentJump in currentJumpList)
                {
                    if (currentJump.SolarSystemID == solaySystemId)
                    {
                        return jump;
                    }
                    else
                    {
                        if (currentJump.Jumps.NotNullOrEmpty())
                        {
                            foreach (var next in currentJump.Jumps)
                            {
                                if (!found.Contains(next.SolarSystemID))
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

        public List<IntelSolarSystemMap> GetAllSolarSystem()
        {
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

        /// <summary>
        /// 按跳数分层所有跳数范围内的星系
        /// 起始层为当前星系
        /// </summary>
        /// <returns></returns>
        public List<IntelSolarSystemMap[]> GroupByJump()
        {
            HashSet<int> foundMaps = new HashSet<int>()
            {
                SolarSystemID
            };
            List<IntelSolarSystemMap[]> group = new List<IntelSolarSystemMap[]>();
            List<IntelSolarSystemMap> currentJumpList = new List<IntelSolarSystemMap>()
            {
                this
            };//当前跳数的星系，开始只有中心星系一个
            List<IntelSolarSystemMap> newJumpList = new List<IntelSolarSystemMap>();//下一跳数的星系
            while (currentJumpList.Count > 0)
            {
                group.Add(currentJumpList.ToArray());
                foreach (var jump in currentJumpList)
                {
                    if (jump.Jumps.NotNullOrEmpty())
                    {
                        foreach (var nextJump in jump.Jumps)
                        {
                            if (!foundMaps.Contains(nextJump.SolarSystemID))
                            {
                                foundMaps.Add(nextJump.SolarSystemID);
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
            return group;
        }
    }
}
