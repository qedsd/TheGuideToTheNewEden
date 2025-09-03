using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using ZKB.NET.Models.Killmails;

namespace ZKB.NET.Models.KillStream
{
    public class SKBDetailQ
    {
        public int KillID { get; set; }
        public KillmailDetail Killmail { get; set; }
        public ZkbInfo Zkb { get; set; }

        public SKBDetail ToSKBDetail()
        {
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var model =  JsonConvert.DeserializeObject<SKBDetail>(JsonConvert.SerializeObject(Killmail), deserializeSettings);
            model.Zkb = Zkb;
            return model;
        }
    }
}
