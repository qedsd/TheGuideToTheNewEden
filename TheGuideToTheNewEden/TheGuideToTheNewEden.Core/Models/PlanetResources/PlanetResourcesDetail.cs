using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlanetResources
{
    /// <summary>
    /// 星系资源具体信息
    /// </summary>
    public class PlanetResourcesDetail
    {
        public DBModels.PlanetResources PlanetResources { get; set; }
        public DBModels.MapDenormalize MapDenormalize { get; set; }
        public long MagmaticGas { get => PlanetResources?.TypeId == 81143 ? PlanetResources.AmountPerCycle : 0; }
        public long SuperionicIce { get => PlanetResources?.TypeId == 81144 ? PlanetResources.AmountPerCycle : 0; }
        public bool ContainResource
        {
            get
            {
                if(PlanetResources != null)
                {
                    return PlanetResources.Power != 0 || PlanetResources.Workforce != 0 || PlanetResources.AmountPerCycle != 0;
                }
                return false;
            }
        }
    }
}
