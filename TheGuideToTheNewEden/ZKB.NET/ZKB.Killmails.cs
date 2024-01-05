using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Killmails;
using ZKB.NET.Models.Statistics;

namespace ZKB.NET
{
    public static partial class ZKB
    {
        private static string ApiUrl
        {
            get => $"{Config.ApiUrl.TrimEnd('/')}/kills/";
        }
        public static async Task<List<ZKillmaill>> GetKillmaillAsync(ParamModifierData[] paramModifiers, params TypeModifier[] typeModifiers)
        {
            StringBuilder url = new StringBuilder(ApiUrl);
            if (paramModifiers != null)
            {
                foreach(var p in paramModifiers)
                {
                    url.Append(ToStartLower(p.Modifier.ToString()));
                    url.Append('/');
                    url.Append(p.Param);
                    url.Append('/');
                }
            }
            url.Append(ToUrl(typeModifiers));
            return await GetKillmaillAsync(url.ToString());
        }
        public static async Task<List<ZKillmaill>> GetKillmaillAsync(ParamModifier paramModifier, string param, params TypeModifier[] typeModifiers)
        {
            StringBuilder url = new StringBuilder(ApiUrl);
            url.Append(ToStartLower(paramModifier.ToString()));
            url.Append('/');
            url.Append(param);
            url.Append('/');
            url.Append(ToUrl(typeModifiers));
            return await GetKillmaillAsync(url.ToString());
        }

        private static string ToUrl(params TypeModifier[] typeModifiers)
        {
            StringBuilder url = new StringBuilder();
            if (typeModifiers != null)
            {
                foreach (var p in typeModifiers)
                {
                    url.Append(ToStartLower(p.ToString().Replace('_', '-')));
                    url.Append('/');
                    if(p == TypeModifier.Awox)
                    {
                        url.Append('1');
                        url.Append('/');
                    }
                    if (p == TypeModifier.Npc)
                    {
                        url.Append('1');
                        url.Append('/');
                    }
                }
            }
            return url.ToString();
        }
        private static async Task<List<ZKillmaill>> GetKillmaillAsync(string url)
        {
            string json = await HttpHelper.GetZKBAsync(url);
            return JsonConvert.DeserializeObject<List<ZKillmaill>>(json);
        }
    }
    public struct ParamModifierData
    {
        public ParamModifierData(ParamModifier paramModifier, string param)
        {
            Modifier = paramModifier;
            Param = param;
        }
        public ParamModifier Modifier;
        public string Param;
    }
    public enum TypeModifier
    {
        Kills,
        Losses,
        W_space,
        Solo,
        Finalblow_only,
        Awox,
        Npc
    }
    public enum ParamModifier
    {
        CharacterID,
        CorporationID,
        AllianceID,
        FactionID,
        ShipTypeID,
        GroupID,
        SystemID,
        RegionID,
        WarID,
        IskValue,
        KillID,
        Page,
        Year,
        Month,
        PastSeconds,
    }
}
