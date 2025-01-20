using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    public class EntityStatistic
    {
        public EntityStatisticType StatisticType
        {
            get
            {
                switch(Type)
                {
                    case "characterID":return EntityStatisticType.Character;
                    case "corporationID": return EntityStatisticType.Corporation;
                    case "allianceID": return EntityStatisticType.Alliance;
                    case "factionID": return EntityStatisticType.Faction;
                    case "shipTypeID": return EntityStatisticType.ShipType;
                    case "groupID": return EntityStatisticType.Group;
                    case "solarSystemID": return EntityStatisticType.SolarSystem;
                    case "regionID": return EntityStatisticType.Region;
                    default:return EntityStatisticType.Unknown;
                }
            }
        }
        /// <summary>
        /// characterID/corporationID/allianceID/factionID/shipTypeID/groupID/solarSystemID/regionID
        /// </summary>
        public string Type { get; set; }

        public int Id { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public bool Reset { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public List<KillStatistic> TopAllTime { get; set; }

        /// <summary>
        /// 将groups转成GroupData
        /// </summary>
        [JsonProperty("groups")]
        private JObject GroupsObj
        {
            set
            {
                if(value != null && value.Count > 0)
                {
                    var groupDatas = new List<GroupData>();
                    foreach (var p in value)
                    {
                        groupDatas.Add(p.Value.ToObject<GroupData>());
                    }
                    Groups = groupDatas;
                }
                else
                {
                    Groups = null;
                }
            }
        }

        /// <summary>
        /// 按船分类统计
        /// </summary>
        [JsonIgnore]
        public List<GroupData> Groups { get; set; }

        /// <summary>
        /// 将months转成MonthData
        /// </summary>
        [JsonProperty("months")]
        private JObject MonthsObj
        {
            set
            {
                if (value != null && value.Count > 0)
                {
                    var datas = new List<MonthData>();
                    foreach (var p in value)
                    {
                        datas.Add(p.Value.ToObject<MonthData>());
                    }
                    Months = datas;
                }
                else
                {
                    Months = null;
                }
            }
        }
        [JsonIgnore]
        public List<MonthData> Months { get; set; }


        /// <summary>
        /// 将labels转成LabelData
        /// </summary>
        [JsonProperty("labels")]
        public JObject LabelsObj
        {
            set
            {
                if (value != null && value.Count > 0)
                {
                    var datas = new List<LabelData>();
                    foreach (var p in value)
                    {
                        var data = p.Value.ToObject<LabelData>();
                        data.Label = p.Key;
                        datas.Add(data);
                    }
                    Labels = datas;
                }
                else
                {
                    Labels = null;
                }
            }
        }

        public List<LabelData> Labels { get; set; }


        /// <summary>
        /// 损失数
        /// 不局限于船
        /// </summary>
        [JsonProperty("shipsLost")]
        public int ItemLost { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int PointsLost { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("iskLost")]
        public long ISKLost { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int AttackersLost { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int SoloLosses { get; set; }

        /// <summary>
        /// 损失数量
        /// 不仅限于船
        /// </summary>
        [JsonProperty("shipsDestroyed")]
        public int ItemDestroyed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int PointsDestroyed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("iskDestroyed")]
        public long ISKDestroyed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int AttackersDestroyed { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int SoloKills { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public int Epoch { get; set; }

        /// <summary>
        /// 危险值
        /// </summary>
        public int DangerRatio { get; set; }

        /// <summary>
        /// 群体活动比例
        /// 越高表示越喜欢ppppvpppp
        /// 越低越喜欢pvp
        /// </summary>
        public int GangRatio { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public int AllTimeSum { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public int NextTopRecalc { get; set; }

        /// <summary>
        /// 历史最高击杀kb的id
        /// </summary>
        public List<int> TopIskKills { get; set; }

        /// <summary>
        /// 是否有超期（有超期击杀别人或超期被别人击杀记录）
        /// </summary>
        public bool HasSupers { get; set; }

        /// <summary>
        /// 超期驾驶员及其相关超期击杀数量
        /// </summary>
        public Super Supers { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public bool UpdatingSupers { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Activepvp Activepvp { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public EntityInfo Info { get; set; }

        /// <summary>
        /// 暂无用处
        /// </summary>
        public List<TopKill> TopLists { get; set; }

        /// <summary>
        /// 最近7天最高击杀
        /// </summary>
        [JsonProperty("topIskKillIDs")]
        public List<int> TopIskKills7d { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public JObject Activity { get; set; }

    }

    public enum EntityStatisticType
    {
        Unknown,
        Character,
        Corporation,
        Alliance,
        Faction,
        ShipType,
        Group,
        SolarSystem,
        Region
    }
}
