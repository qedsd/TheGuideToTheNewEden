using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using ZKB.NET.Models.Killmails;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class CargoItemInfo
    {
        public CargoItem CargoItem { get; set; }
        public InvType Type { get; set; }
        public List<CargoItemInfo> SubItems { get; set; }
        public CargoItemInfo(CargoItem cargoItem)
        {
            CargoItem = cargoItem;
        }
    }
}
