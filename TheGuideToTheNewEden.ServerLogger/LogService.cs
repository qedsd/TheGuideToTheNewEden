

using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.ServerLogger.DbModels;
using TheGuideToTheNewEden.ServerLogger.Models;

namespace TheGuideToTheNewEden.ServerLogger
{
    internal class LogService
    {
        public static int Duration { get; set; }

        static System.Timers.Timer ExecuteTimer;
        public static void Begin()
        {
            DbService.Init();
            ExecuteTime(null, null);
            ExecuteTimer = new System.Timers.Timer(Duration * 1000);
            ExecuteTimer.Elapsed += new System.Timers.ElapsedEventHandler(ExecuteTime);
            ExecuteTimer.AutoReset = true;
            ExecuteTimer.Enabled = true;
            ExecuteTimer.Start();
        }
        static int Count = 0;
        static void ExecuteTime(object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                GetTranquilityServerStatus();
                GetSerenityServerStatus();
                GetTiebaInfo();
                Console.WriteLine($"{DateTime.Now} 统计次数 {++Count}");
            }
            catch (Exception er) 
            {
                Console.WriteLine(er.ToString());
            }
        }

        static async void GetTranquilityServerStatus()
        {
            try
            {
                string result = await HttpHelper.GetAsync("https://esi.evetech.net/latest/status/?datasource=tranquility");
                if (string.IsNullOrEmpty(result))
                    return;
                TranquilityServerStatus status = Newtonsoft.Json.JsonConvert.DeserializeObject<TranquilityServerStatus>(result);
                status.CreatedTime = DateTime.Now;
                if (status != null)
                {
                    DbService.Insert(status);
                }
            }
            catch (Exception ex) 
            { 
                Console.WriteLine(ex.ToString());
            }
        }

        static async void GetSerenityServerStatus()
        {
            try
            {
                string result = await HttpHelper.GetAsync("https://esi.evepc.163.com/latest/status/?datasource=tranquility");
                if (string.IsNullOrEmpty(result))
                    return;
                SerenityServerStatus status = Newtonsoft.Json.JsonConvert.DeserializeObject<SerenityServerStatus>(result);
                status.CreatedTime = DateTime.Now;
                if (status != null)
                {
                    DbService.Insert(status);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static async void GetTiebaInfo()
        {
            try
            {
                var tiebaInfo = await GetTiebaInfo("eve欧服");
                TranquilityTiebaInfo tranquilityTiebaInfo = new TranquilityTiebaInfo()
                {
                    CreatedTime = DateTime.Now,
                    Themes = tiebaInfo.themes,
                    Replys = tiebaInfo.replys,
                    Members = tiebaInfo.members
                };
                DbService.Insert(tranquilityTiebaInfo);
                tiebaInfo = await GetTiebaInfo("eve");
                SerenityTiebaInfo serenityTiebaInfo = new SerenityTiebaInfo()
                {
                    CreatedTime = DateTime.Now,
                    Themes = tiebaInfo.themes,
                    Replys = tiebaInfo.replys,
                    Members = tiebaInfo.members
                };
                DbService.Insert(serenityTiebaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        struct TiebaInfoStruct
        {
            public int themes;
            public int replys;
            public int members;
        }

        static async Task<TiebaInfoStruct> GetTiebaInfo(string tiebaName)
        {
            string result = await HttpHelper.GetAsync("https://tieba.baidu.com/f?kw=" + tiebaName);
            var ls = WebCrawlerHelper.GetLabelContent(result, "div", "th_footer_l");
            TiebaInfoStruct tiebaInfo = new TiebaInfoStruct();
            if (ls != null && ls.Count > 0)
            {
                var ls2 = WebCrawlerHelper.GetLabelContent(ls[0], "span", "red_text");
                if (ls2 != null && ls2.Count == 3)
                {
                    int.TryParse(WebCrawlerHelper.GetTitleContent(ls2[0], "span"), out tiebaInfo.themes);
                    int.TryParse(WebCrawlerHelper.GetTitleContent(ls2[1], "span"), out tiebaInfo.replys);
                    int.TryParse(WebCrawlerHelper.GetTitleContent(ls2[2], "span"), out tiebaInfo.members);
                }
            }
            return tiebaInfo;
        }
    }
}
