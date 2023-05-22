using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal class ActivationService
    {
        public static void Init()
        {
            SettingService.Load();
            CoreConfig.MainDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "main.db");
            CoreConfig.LocalDbPath = LocalDbSelectorService.Value;
            CoreConfig.DefaultGameServer = GameServerSelectorService.Value;
            CoreConfig.PlayerStatusApi = PlayerStatusService.Value;
            CoreConfig.SolarSystemMapPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "SolarSystemMap.json");
            CoreConfig.InitDb();
            CharacterService.Init();
            //TODO:仅供测试
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MjEzODE5MkAzMjMxMmUzMjJlMzNBbG13Yy94WkdZQ3RkQ29obHQ0dmFSYnUvaFB2TWFycTFqanZsUWFMNWpvPQ==;Mgo+DSMBaFt+QHJqVk1lQ1NNaV1CX2BZf1l1Q2lafU4BCV5EYF5SRHNdQF1iSnhQdEJgX3g=;Mgo+DSMBMAY9C3t2VFhiQlJPc0BAVHxLflF1VWJTfFh6d1dWESFaRnZdQV1mS3xTd0ZlWn1ZdXVS;Mgo+DSMBPh8sVXJ1S0R+X1pBaV1LQmFJfFBmRGlaeVRzdkU3HVdTRHRcQlhhS35SdUNgW3hYdHM=;MjEzODE5NkAzMjMxMmUzMjJlMzNkRW5lY0RGWVlLVlJmWHRNL2VKMDYrYW9HSnl6ZlpRaENWa3NQSDc1Y0lzPQ==;NRAiBiAaIQQuGjN/V0d+Xk9HfVpdX2pWfFN0RnNYdV12flZEcC0sT3RfQF5jTHxRdkRgX3xcd31SQQ==;ORg4AjUWIQA/Gnt2VFhiQlJPc0BAVHxLflF1VWJTfFh6d1dWESFaRnZdQV1mS3xTd0ZlWn1WcXRS;MjEzODE5OUAzMjMxMmUzMjJlMzNPb0gxNU12c0Q5Y2tLak5JdFJGU28zaERnM3FCUnFtZnluOEJLTXQ3NEpVPQ==;MjEzODIwMEAzMjMxMmUzMjJlMzNYcnorYWV1UjUzblp3UDNBRWk3SGxna1ZhMDN5Y21BOHkrSVZrSW5FZlVNPQ==;MjEzODIwMUAzMjMxMmUzMjJlMzNhR2dma3g2amhva0pTRWh5cnZmRGZvYkl5SE9vZE1ZaWtwdFM5d1ZCbWJRPQ==;MjEzODIwMkAzMjMxMmUzMjJlMzNsTy9KVzdUSEtRODJZMGtzd09qcjZJVUtJaktpNXVCT1hycmpkZ3QvZnY0PQ==;MjEzODIwM0AzMjMxMmUzMjJlMzNBbG13Yy94WkdZQ3RkQ29obHQ0dmFSYnUvaFB2TWFycTFqanZsUWFMNWpvPQ==");
        }
    }
}
