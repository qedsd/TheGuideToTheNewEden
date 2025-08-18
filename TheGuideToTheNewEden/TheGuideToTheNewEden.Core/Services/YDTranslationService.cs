using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlSugar;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models.Translation;

namespace TheGuideToTheNewEden.Core.Services
{
    public class YDTranslationService : ITranslationService
    {
        internal class YDResultModel
        {
            public string ErrorCode {  get; set; }
            public string Query { get; set; }
            public string[] Translation {  get; set; }
            public string L {  get; set; }
        }
        public static string AppKey = null;
        public static string AppSerct = null;
        public static string VocabId = null;

        /// <summary>
        /// 传入应用ID、应用密钥、字典
        /// </summary>
        /// <param name="strings"></param>
        public static void Init(params string[] strings)
        {
            AppKey = strings[0];
            AppSerct = strings[1];
            VocabId = strings.Length > 2 ? strings[2] : null;
        }

        public async Task<TranslationResult> Translate(string text, string from, string to)
        {
            var paramsMap = new Dictionary<string, string[]>() {
                { "q", new string[]{text}},
                {"from", new string[]{from}},
                {"to", new string[]{to}},
                {"vocabId", new string[]{VocabId}}
            };
            AddAuthParams(AppKey, AppSerct, paramsMap);
            Dictionary<string, string[]> header = new Dictionary<string, string[]>() { { "Content-Type", new string[] { "application/x-www-form-urlencoded" } } };
            try
            {
                string result = await HttpPost("https://openapi.youdao.com/api", header, paramsMap, "application/json");
                var ydResult = JsonConvert.DeserializeObject<YDResultModel>(result);
                return new TranslationResult()
                {
                    Success = true,
                    Result = string.Join(";", ydResult.Translation),
                    Query = ydResult.Query,
                    From = ydResult.L.Split('2')[0],
                    To = ydResult.L.Split('2')[1],
                };
            }
            catch (Exception ex)
            {
                return new TranslationResult() { Success = false, ErrorMsg = ex.Message};
            }
        }

        public static void AddAuthParams(string appKey, string appSecret, Dictionary<string, string[]> paramsMap)
        {
            string q = "";
            string[] qArray;
            if (paramsMap.ContainsKey("q"))
            {
                qArray = paramsMap["q"];
            }
            else
            {
                qArray = paramsMap["img"];
            }
            foreach (var item in qArray)
            {
                q += item;
            }
            string salt = System.Guid.NewGuid().ToString();
            string curtime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + "";
            string sign = CalculateSign(appKey, appSecret, q, salt, curtime);
            paramsMap.Add("appKey", new string[] { appKey });
            paramsMap.Add("salt", new string[] { salt });
            paramsMap.Add("curtime", new string[] { curtime });
            paramsMap.Add("signType", new string[] { "v3" });
            paramsMap.Add("sign", new string[] { sign });
        }

        /*
            计算鉴权签名 -
            计算方式 : sign = sha256(appKey + input(q) + salt + curtime + appSecret)
        
            @param appKey    您的应用ID
            @param appSecret 您的应用密钥
            @param q         请求内容
            @param salt      随机值
            @param curtime   当前时间戳(秒)
            @return 鉴权签名sign
        */
        public static string CalculateSign(string appKey, string appSecret, string q, string salt, string curtime)
        {
            string strSrc = appKey + GetInput(q) + salt + curtime + appSecret;
            return Encrypt(strSrc);
        }

        private static string Encrypt(string strSrc)
        {
            Byte[] inputBytes = Encoding.UTF8.GetBytes(strSrc);
            HashAlgorithm algorithm = new SHA256CryptoServiceProvider();
            Byte[] hashedBytes = algorithm.ComputeHash(inputBytes);
            return BitConverter.ToString(hashedBytes).Replace("-", "");
        }

        private static string GetInput(string q)
        {
            if (q == null)
            {
                return "";
            }
            int len = q.Length;
            return len <= 20 ? q : q.Substring(0, 10) + len + q.Substring(len - 10, 10);
        }

        public static async System.Threading.Tasks.Task<string> HttpPost(string url, Dictionary<string, string[]> header, Dictionary<string, string[]> param, string expectContentType)
        {
            StringBuilder content = new StringBuilder();
            using (HttpClient client = new HttpClient())
            {
                if (param != null)
                {
                    int i = 0;
                    foreach (var p in param)
                    {
                        foreach (var v in p.Value)
                        {
                            if (i > 0)
                            {
                                content.Append("&");
                            }
                            content.AppendFormat("{0}={1}", p.Key, System.Web.HttpUtility.UrlEncode(v));
                            i++;
                        }

                    }
                }

                var para = new StringContent(content.ToString());
                if (header != null)
                {
                    para.Headers.Clear();
                    foreach (var h in header)
                    {
                        foreach (var v in h.Value)
                        {
                            para.Headers.Add(h.Key, v);
                        }
                    }
                }
                var res = await client.PostAsync(url, para);
                res.EnsureSuccessStatusCode();
                IEnumerable<string> contentTypeHeader;
                var suc = res.Content.Headers.TryGetValues("Content-Type", out contentTypeHeader);
                string resStr = System.Text.Encoding.UTF8.GetString(res.Content.ReadAsByteArrayAsync().Result);
                if (suc && !((string[])contentTypeHeader)[0].Contains(expectContentType))
                {
                    throw new Exception($"Not expect content type: {resStr}");
                }
                return resStr;
            }
        }
    }
}
