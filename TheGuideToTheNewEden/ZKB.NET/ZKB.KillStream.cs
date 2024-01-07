using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.KillStream;
using static System.Collections.Specialized.BitVector32;

namespace ZKB.NET
{
    public static partial class ZKB
    {
        public static KillStream GetKillStream()
        {
            return new KillStream(Config.WWS);
        }

        public static async Task<KillStream> SubKillStreamAsync()
        {
            KillStream killStream = new KillStream(Config.WWS);
            await killStream.ConnectAsync();
            object msg = new
            {
                action = "sub",
                channel="killstream"
            };
            await killStream.SendMessageAsync(JsonConvert.SerializeObject(msg));
            return killStream;
        }

        public static async Task<KillStream> SubKillStreamAsync(KillStreamFilter filter, string id)
        {
            KillStream killStream = new KillStream(Config.WWS);
            await killStream.ConnectAsync();
            string filterStr = ToStartLower(filter.ToString());
            object filterObj = string.IsNullOrEmpty(id) ? filterStr :
                                new
                                {
                                    filterStr = id
                                };
            object msg = new
            {
                action = "sub",
                channel = filterObj
            };
            await killStream.SendMessageAsync(JsonConvert.SerializeObject(msg));
            return killStream;
        }


        public enum KillStreamFilter
        {
            Character,Corporation,Alliance,Faction,Ship,Group,System,Constellation,Region,Location,Label,All
        }
    }
}
