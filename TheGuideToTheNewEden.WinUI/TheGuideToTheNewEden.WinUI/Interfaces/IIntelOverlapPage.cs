﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Interfaces
{
    internal interface IIntelOverlapPage
    {
        void Init(BaseWindow window, Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap);
        void Intel(EarlyWarningContent content);
        void Clear(List<int> systemIds);
        void Downgrade(List<int> systemIds);
        void Dispose();
    }
}