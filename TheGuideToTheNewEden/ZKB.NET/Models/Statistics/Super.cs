using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Statistics
{
    public class Super
    {
        [JsonProperty("titans")]
        public SuperData Titans { get; set; }
        [JsonProperty("supercarriers")]
        public SuperData Supercarriers { get; set; }
    }
    public class SuperData
    {
        public string Title { get; set; }
        /// <summary>
        /// id指定characterID
        /// </summary>
        [JsonProperty("data")]
        public List<KillData> Datas { get; set; }
    }
}
