using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.WinUI.Interfaces
{
    public interface IIntelOverlapPage
    {
        void Init(BaseWindow window, ChannelIntelSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap);
        void Intel(EarlyWarningContent content);
        void Clear(List<int> systemIds);
        void Clear();
        void Downgrade(List<int> systemIds);
        void Dispose();
        void UpdateUI();
        void UpdateHome(IntelSolarSystemMap intelMap);
    }
}
