using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Collections;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;
using ZKB.NET.Models.Killmails;

namespace ZKB.NET.Models.KillStream
{
    public class KillStreamR2Z2
    {
        public delegate void MessageEventHandler(object sender, SKBDetail detail, string sourceData);
        public event MessageEventHandler OnMessage;
        public string Url { get; private set; }
        private long _latestSeq = 0;
        public KillStreamR2Z2()
        {
            Url = "https://r2z2.zkillboard.com/ephemeral/sequence.json";
        }
        private Timer _timer;
        public async Task<bool> ConnectAsync()
        {
            var content = await GetLatestSeq();
            if (content == null)
            {
                return false;
            }
            else
            {
                SKBDetailR2Z2 detail = null;
                long searchSeq = content.Sequence + 1;
                int step = 10;
                while (true)
                {
                    try
                    {
                        detail = await GetSKBDetail(searchSeq);
                    }
                    catch(Exception)
                    {
                        return false;
                    }
                    if (detail == null)
                    {
                        if(step == 1)
                        {
                            break;
                        }
                        searchSeq -= step;//返回上一次起点逐个递增查找
                        if (step == 10)
                        {
                            step = 5;
                        }
                        else
                        {
                            step = 1;
                        }
                    }
                    searchSeq += step;
                }
                _latestSeq = searchSeq;
                _timer = new Timer()
                {
                    Interval = 6000,
                    AutoReset = false,
                };
                _timer.Elapsed += Timer_Elapsed;
                _timer.Start();
                return true;
            }
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e) 
        {
            try
            {
                while(true)
                {
                    var detail = await GetSKBDetail(_latestSeq);
                    if (detail != null)
                    {
                        _latestSeq++;
                        OnMessage?.Invoke(this, detail.ToSKBDetail(), JsonConvert.SerializeObject(detail));
                    }
                    else
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
            }
            finally
            {
                _timer?.Start();
            }
        }
        public void Close()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        private async Task<SeqMsg> GetLatestSeq()
        {
            try
            {
                string str = await HttpGetAsync(Url);
                if (!string.IsNullOrEmpty(str))
                {
                    return JsonConvert.DeserializeObject<SeqMsg>(str);
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }
        private async Task<SKBDetailR2Z2> GetSKBDetail(long seqId)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    HttpClient httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var response = await httpClient.GetAsync($"https://r2z2.zkillboard.com/ephemeral/{seqId}.json");
                    if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        string json = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<SKBDetailR2Z2>(json);
                    }
                }
                catch (Exception ex)
                {
                    if (i == 2)
                    {
                        throw ex;
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
            }
            return null;
        }

        private async Task<string> HttpGetAsync(string url)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public event EventHandler<Exception> OnError;

        internal class SeqMsg
        {
            public long Sequence { get; set; }
        }
    }
}
