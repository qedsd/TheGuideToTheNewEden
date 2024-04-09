using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.Core.Intel
{
    public class ZKBIntel
    {
        private Models.Map.IntelSolarSystemMap _intelMap { get; set; }
        private EarlyWarningSetting _setting;
        public ZKBIntel(EarlyWarningSetting setting, Models.Map.IntelSolarSystemMap map)
        {
            _setting = setting;
            _intelMap = map;
        }
        public async Task Start()
        {
            await Services.ZKBStreamService.Current.Sub();
            Services.ZKBStreamService.Current.OnMessage += KillStream_OnMessage;
        }
        public void Stop()
        {
            Services.ZKBStreamService.Current.UnSub();
            Services.ZKBStreamService.Current.OnMessage -= KillStream_OnMessage;
        }
        public string GetListener()
        {
            return _setting.Listener;
        }
        private void KillStream_OnMessage(object sender, SKBDetail detail, string sourceData)
        {
            int jumps = _intelMap.JumpsOf(detail.SolarSystemId);
            if (jumps != -1)
            {
                var span = DateTime.UtcNow - detail.KillmailTime;
                if (span.TotalSeconds < _setting.KBTime && !detail.Zkb.Npc)
                {
                    var info = Helpers.KBHelpers.CreateKBItemInfo(detail);
                    var content = new EarlyWarningContent()
                    {
                        Content = $"{info.Type.TypeName}({detail.Attackers.Count})",
                        Time = detail.KillmailTime,
                        SolarSystemId = detail.SolarSystemId,
                        SolarSystemName = info.SolarSystem.SolarSystemName,
                        IntelType = Enums.IntelChatType.Intel,
                        IntelMap = _intelMap,
                        Jumps = jumps
                    };
                    OnWarningUpdate?.Invoke(this, content);
                }
            }
        }
        /// <summary>
        /// 预警更新
        /// </summary>
        public event EventHandler<EarlyWarningContent> OnWarningUpdate;
    }
}
