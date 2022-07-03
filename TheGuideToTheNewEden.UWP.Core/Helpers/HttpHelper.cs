using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Net.Http;
using ICSharpCode.SharpZipLib.GZip;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class HttpHelper
    {
        /// <summary>
        /// 使用post方法异步请求
        /// </summary>
        /// <param name="url">目标链接</param>
        /// <param name="data">发送的参数json</param>
        /// <returns>返回的字符串</returns>
        public static async Task<string> PostJsonAsync(string url, string data, Dictionary<string, string> header = null, bool Gzip = false)
        {
            HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false });
            HttpContent content = new StringContent(data);
            if (header != null)
            {
                client.DefaultRequestHeaders.Clear();
                foreach (var item in header)
                {
                    client.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }
            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody;
                    if (Gzip)
                    {
                        GZipInputStream inputStream = new GZipInputStream(await response.Content.ReadAsStreamAsync());
                        responseBody = new StreamReader(inputStream).ReadToEnd();
                    }
                    else
                    {
                        responseBody = await response.Content.ReadAsStringAsync();
                    }
                    return responseBody;
                }
                else
                {
                    return null;
                }
            }
            catch(Exception)
            {
                return null;
            }
        }


        public static async Task<string> PostAsync(string url, Dictionary<string, string> form)
        {
            HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false });
            HttpContent content = new FormUrlEncodedContent(form);
            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 使用get方法异步请求
        /// </summary>
        /// <param name="url">目标链接</param>
        /// <returns>返回的字符串</returns>
        public static async Task<string> GetAsync(string url, Dictionary<string, string> header = null, bool Gzip = false)
        {
            HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies = false });
            if (header != null)
            {
                client.DefaultRequestHeaders.Clear();
                foreach (var item in header)
                {
                    client.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
            }
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                string responseBody;
                if (Gzip)
                {
                    GZipInputStream inputStream = new GZipInputStream(await response.Content.ReadAsStreamAsync());
                    responseBody = new StreamReader(inputStream).ReadToEnd();
                }
                else
                {
                    responseBody = await response.Content.ReadAsStringAsync();
                }
                return responseBody;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
