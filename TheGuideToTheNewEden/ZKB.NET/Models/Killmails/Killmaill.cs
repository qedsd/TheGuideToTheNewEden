using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZKB.NET.Models.Killmails
{
    public class Killmaill
    {
        [JsonProperty("killmail_hash")]
        public string Hash { get; set; }

        [JsonProperty("killmail_id")]
        public int Id { get; set; }
    }
}
