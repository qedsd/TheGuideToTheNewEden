using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    public class KillAndLoseDataBase
    {
        /// <summary>
        /// 击杀的数量
        /// 不局限于船
        /// </summary>
        [JsonProperty("shipsDestroyed")]
        public int ItemDestroyed { get; set; }

        /// <summary>
        /// 击杀获得的点数
        /// </summary>
        public int PointsDestroyed { get; set; }

        /// <summary>
        /// isk击杀量
        /// </summary>
        public long IskDestroyed { get; set; }

        /// <summary>
        /// 损失的数量
        /// </summary>
        [JsonProperty("shipsLost")]
        public int ItemLost { get; set; }

        /// <summary>
        /// 损失的点数
        /// </summary>
        public int PointsLost { get; set; }

        /// <summary>
        /// 损失的isk
        /// </summary>
        public long IskLost { get; set; }
    }
}
