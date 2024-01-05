using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    /// <summary>
    /// 击杀数量统计--TopAllTime
    /// </summary>
    public class KillStatistic
    {
        /// <summary>
        /// 统计类型：character、corporation、alliance、faction、ship、system
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// 具体击杀数量
        /// </summary>
        [JsonProperty("data")]
        public List<KillData> Datas { get; set; }
    }
}
