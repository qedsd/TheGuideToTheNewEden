using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.DevTools.Translation
{
    internal static class OutputRegionAndSystemName
    {
        public static List<TranslationItem> Start()
        {
            List<TranslationItem> translationItems = new List<TranslationItem>();
            TheGuideToTheNewEden.Core.Config.NeedLocalization = false;
            var enRegions = Core.Services.DB.MapRegionService.QueryAll();
            TheGuideToTheNewEden.Core.Config.NeedLocalization = true;
            var zhRegions = Core.Services.DB.MapRegionService.QueryAll().ToDictionary(p=>p.RegionID);
            foreach (var region in enRegions)
            {
                var targetZhRegion = zhRegions[region.RegionID];
                if (targetZhRegion.RegionName == region.RegionName)
                {
                    continue;
                }
                translationItems.Add(new TranslationItem(region.RegionName, targetZhRegion.RegionName));
            }

            TheGuideToTheNewEden.Core.Config.NeedLocalization = false;
            var enSystems = Core.Services.DB.MapSolarSystemService.QueryAll();
            TheGuideToTheNewEden.Core.Config.NeedLocalization = true;
            var zhSystems = Core.Services.DB.MapSolarSystemService.QueryAll().ToDictionary(p => p.SolarSystemID);
            foreach (var system in enSystems)
            {
                var target = zhSystems[system.SolarSystemID];
                if (target.SolarSystemName == system.SolarSystemName)
                {
                    continue;
                }
                translationItems.Add(new TranslationItem(system.SolarSystemName, target.SolarSystemName));
            }
            return translationItems;
        }
    }
}
