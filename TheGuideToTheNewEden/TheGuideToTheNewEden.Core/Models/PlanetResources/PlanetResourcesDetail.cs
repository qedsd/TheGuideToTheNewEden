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
        //public DBModels.InvName ItemName { get; set; }
        public DBModels.InvType ItemType { get; set; }
    }
}
