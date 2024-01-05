using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Killmails
{
    /// <summary>
    /// 由zkb赋予的killmail额外信息
    /// </summary>
    public class ZkbInfo
    {
        /// <summary>
        /// 击杀位置
        /// 星门、卫星、小行星带等任意位置
        /// 对应数据库mapDenormalize->typeID
        /// </summary>
        public int LocationID { get; set; }

        /// <summary>
        /// ccp hash
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// 装配估价？意义未知
        /// </summary>
        public double FittedValue { get; set; }

        /// <summary>
        /// 掉落价值
        /// </summary>
        public double DroppedValue { get; set; }

        /// <summary>
        /// 损失价值--即黑掉的部分
        /// </summary>
        public double DestroyedValue { get; set; }

        /// <summary>
        /// 合计价值-掉落+损失
        /// </summary>
        public double TotalValue { get; set; }

        /// <summary>
        /// zkb点数
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// 是否是被npc打死的
        /// </summary>
        public bool Npc { get; set; }

        /// <summary>
        /// 不算npc，如玩家+npc一块击杀的也算solo，仅有npc（npc == true）的不算
        /// </summary>
        public bool Solo { get; set; }

        /// <summary>
        /// 不明
        /// </summary>
        public bool Awox { get; set; }

        public string Esi { get; set; }
        public string Url { get; set;}
    }
}
