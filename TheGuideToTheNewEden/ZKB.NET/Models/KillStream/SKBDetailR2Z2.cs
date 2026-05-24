using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using ZKB.NET.Models.Killmails;

namespace ZKB.NET.Models.KillStream
{
    public class SKBDetailR2Z2
    {
        [JsonProperty("killmail_id")]
        public int KillmailID { get; set; }

        public string Hash { get; set; }

        [JsonProperty("esi")]
        public KillmailDetail Killmail { get; set; }

        public ZkbInfo Zkb { get; set; }

        [JsonProperty("uploaded_at")]
        public long UploadedAt { get; set; }

        [JsonProperty("sequence_id")]
        public long Sequence_id { get; set; }

        public SKBDetail ToSKBDetail()
        {
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var model = JsonConvert.DeserializeObject<SKBDetail>(JsonConvert.SerializeObject(Killmail), deserializeSettings);
            model.Zkb = Zkb;
            return model;
        }
    }
}
