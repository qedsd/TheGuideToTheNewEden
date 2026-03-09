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
    public class KillStreamQ
    {
        public delegate void MessageEventHandler(object sender, SKBDetail detail, string sourceData);
        public event MessageEventHandler OnMessage;
        public string Url { get; private set; }
        public KillStreamQ(string url)
        {
            Url = $"{url}?queueID=ZKB.NET_{Guid.NewGuid()}?ttw=1";
        }
        private Timer _timer;
        public async Task<bool> ConnectAsync()
        {
            var content = await GetContent();
            if (content == null)
            {
                return false;
            }
            else
            {
                _timer = new Timer()
                {
                    Interval = 1000,
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
                string content = await GetContent();
                if (!string.IsNullOrEmpty(content))
                {
                    var obj = JsonConvert.DeserializeObject<KillStreamQRoot>(content);
                    if (obj?.Package != null)
                    {
                        if (obj.Package.Killmail == null)
                        {
                            obj.Package.Killmail = await GetKillMail(obj.Package.Zkb.Href);
                        }
                        if (obj.Package.Killmail.Attackers == null || obj.Package.Killmail.Attackers.FirstOrDefault(p => p.FinalBlow) == null)
                        {

                        }
                        OnMessage?.Invoke(this, obj.Package.ToSKBDetail(), content);
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
        private async Task<KillmailDetail> GetKillMail(string url)
        {
            for(int i = 0; i < 3; i++)
            {
                try
                {
                    string json = await HttpHelper.GetAsync(url);
                    return JsonConvert.DeserializeObject<KillmailDetail>(json);
                }
                catch(Exception ex)
                {
                    if(i == 2)
                    {
                        throw ex;
                    }
                    else
                    {
                        await Task.Delay(1000);
                    }
                }
            }
            return null;
        }
        public void Close()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        private async Task<string> GetContent()
        {
            try
            {
                HttpClient hc = new HttpClient();
                var response = await hc.GetAsync(Url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
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

        public event EventHandler<Exception> OnError;
    }
}
