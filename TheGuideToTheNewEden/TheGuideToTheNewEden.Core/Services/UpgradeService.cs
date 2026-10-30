using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Models.PlanetResources;

namespace TheGuideToTheNewEden.Core.Services
{
    /// <summary>
    /// 设施升级定义：直接读 SDE 的 <c>sovereigntyUpgrades</c> 表。
    /// <para>
    /// Power / Workforce 使用 SDE 的 allocation；燃料按 fuel.type_id 分流：81143 = 岩浆气，81144 = 超离子冰。
    /// 旧数据库若还没有该表，会补建空表，等待用新 SDE 重新构建数据库。
    /// </para>
    /// </summary>
    public class UpgradeService
    {
        private const int MagmaticGasTypeId = 81143;
        private const int SuperionicIceTypeId = 81144;

        private static UpgradeService current;
        public static UpgradeService Current => current ??= new UpgradeService();

        private List<Upgrade> _upgrades;

        public List<Upgrade> GetUpgrades()
            => _upgrades ??= LoadFromSde();

        /// <summary>清掉缓存，让下一次调用重新查 SDE 库。</summary>
        public void ClearCache() => _upgrades = null;

        private static List<Upgrade> LoadFromSde()
        {
            try
            {
                var rows = DB.SovereigntyUpgradeService.QueryAll();
                var upgrades = new List<Upgrade>(rows.Count);
                foreach (var row in rows)
                {
                    var fuelTypeId = row.FuelTypeID ?? 0;
                    var fuel = row.FuelHourlyUpkeep ?? 0;
                    upgrades.Add(new Upgrade
                    {
                        Id = row.TypeID,
                        Power = row.PowerAllocation ?? 0,
                        Workforce = row.WorkforceAllocation ?? 0,
                        SuperionicIce = fuelTypeId == SuperionicIceTypeId ? fuel : 0,
                        MagmaticGas = fuelTypeId == MagmaticGasTypeId ? fuel : 0,
                        InvType = DB.InvTypeService.QueryType(row.TypeID),
                    });
                }

                return upgrades
                    .OrderBy(p => p.Name ?? p.Id.ToString(), StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<Upgrade>();
            }
        }
    }
}
