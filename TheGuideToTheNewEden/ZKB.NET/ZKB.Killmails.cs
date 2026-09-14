using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZKB.NET.Models.Killmails;
using ZKB.NET.Models.KillStream;
using ZKB.NET.Models.Statistics;

namespace ZKB.NET
{
    public static partial class ZKB
    {
        private static string ApiUrl
        {
            get => $"{Config.ApiUrl.TrimEnd('/')}/kills/";
        }

        /// <summary>拼 /kills/ 的请求地址（修饰符顺序与 zkillboard 要求一致）。</summary>
        private static string BuildKillsUrl(ParamModifierData[] paramModifiers, TypeModifier[] typeModifiers)
        {
            StringBuilder url = new StringBuilder(ApiUrl);
            if (paramModifiers != null)
            {
                foreach (var p in paramModifiers)
                {
                    url.Append(ToStartLower(p.Modifier.ToString()));
                    url.Append('/');
                    url.Append(p.Param);
                    url.Append('/');
                }
            }
            url.Append(ToUrl(typeModifiers));
            return url.ToString();
        }

        public static async Task<List<ZKillmaill>> GetKillmaillAsync(ParamModifierData[] paramModifiers, params TypeModifier[] typeModifiers)
        {
            return await GetKillmaillAsync(BuildKillsUrl(paramModifiers, typeModifiers));
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

        /// <summary>
        /// 取 <b>完整</b> killmail 列表（攻击者 / 受害者 / 货柜 / 时间 / 星系都在响应里）。
        ///
        /// zkillboard 的 <c>/kills/</c> 现在直接返回完整 killmail，因此**不必**再拿每条
        /// <c>killmail_id + hash</c> 去 ESI 逐条换取（旧路径一页 200 条 = 200 次 ESI 调用，
        /// 且 `KBHelpers` 里用 <c>DepthClone</c> 跨模型拷贝会因命名策略不同而拷空）。
        /// 返回模型是 <see cref="SKBDetail"/>（含 <c>zkb</c> 估价信息），可直接交给
        /// <c>KBHelpers.CreateKBItemInfo(SKBDetail)</c> 做本地库富化。
        /// </summary>
        public static async Task<List<SKBDetail>> GetKillmailDetailsAsync(ParamModifierData[] paramModifiers, params TypeModifier[] typeModifiers)
        {
            string json = await HttpHelper.GetZKBAsync(BuildKillsUrl(paramModifiers, typeModifiers));
            return JsonConvert.DeserializeObject<List<SKBDetail>>(json);
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
