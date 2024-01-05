using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Killmails
{
    public class ZKillmaill
    {
        /// <summary>
        /// ccp
        /// </summary>
        [JsonProperty("killmail_id")]
        public int KillmailId { get; set; }
        public ZkbInfo Zkb {  get; set; }
    }
}
