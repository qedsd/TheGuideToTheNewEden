using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlanetResources
{
    public class Upgrade
    {
        public int Id { get; set; }
        public string Name { get => InvType?.TypeName; }
        public long Power { get; set; }

        public long Workforce { get; set; }
        public long SuperionicIce { get; set; }
        public long MagmaticGas { get; set; }
        public DBModels.InvType InvType { get; set; }
    }
}
