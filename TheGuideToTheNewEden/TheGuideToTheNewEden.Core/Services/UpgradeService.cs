using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Models.PlanetResources;

namespace TheGuideToTheNewEden.Core.Services
{
    public class UpgradeService
    {
        private static UpgradeService current;
        public static UpgradeService Current
        {
            get
            {
                if(current == null)
                    current = new UpgradeService();
                return current;
            }
        }

        private static readonly string FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources", "Configs", "UpgradeResources.csv");

        private List<Models.PlanetResources.Upgrade> _upgrades;
        public List<Models.PlanetResources.Upgrade> GetUpgrades()
        {
            if( _upgrades == null )
            {
                _upgrades = new List<Upgrade>();
                var lines = System.IO.File.ReadAllLines(FilePath);
                for(int i =1; i < lines.Length; i++)
                {
                    var arrays = lines[i].Split(',');
                    _upgrades.Add(new Models.PlanetResources.Upgrade()
                    {
                        Id = int.Parse(arrays[0]),
                        Power = long.Parse(arrays[2]),
                        Workforce = long.Parse(arrays[3]),
                        SuperionicIce = long.Parse(arrays[4]),
                        MagmaticGas = long.Parse(arrays[5]),
                        InvType = Services.DB.InvTypeService.QueryType(int.Parse(arrays[0]))
                    });
                }
            }
            return _upgrades;
        }
    }
}
