using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.Core.Extensions;
using System.Collections;
using ZKB.NET.Models.Killmails;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace TheGuideToTheNewEden.WinUI.ViewModels.KB
{
    public class KBDetailViewModel:BaseViewModel
    {
        public KBItemInfo KBItemInfo { get; set; }

        private List<CargoItemInfo> cargoItemInfos;
        public List<CargoItemInfo> CargoItemInfos{get => cargoItemInfos;set=> SetProperty(ref cargoItemInfos, value); }

        private List<AttackerInfo> attackerInfos;
        public List<AttackerInfo> AttackerInfos { get => attackerInfos; set => SetProperty(ref attackerInfos, value); }

        private AttackerInfo finalBlow;
        public AttackerInfo FinalBlow { get => finalBlow; set => SetProperty(ref finalBlow, value); }

        private AttackerInfo topDamage;
        public AttackerInfo TopDamage { get => topDamage; set => SetProperty(ref topDamage, value); }

        private bool solo;
        public bool Solo { get => solo; set => SetProperty(ref solo, value); }

        public KBDetailViewModel()
        {
            
        }
        public void SetData(KBItemInfo kbInfo)
        {
            KBItemInfo = kbInfo;
            Init();
        }
        private async void Init()
        {
            CargoItemInfos = await Task.Run(() => InitCargoItemInfos());
            var attackerInfos = await InitAttackerInfosAsync();
            if(attackerInfos.Count == 1)
            {
                Solo = true;
            }
            else
            {
                var finalBlow = KBItemInfo.GetFinalBlow();
                FinalBlow = attackerInfos.FirstOrDefault(p => p.Attacker == finalBlow);
                var topDamage = KBItemInfo.GetTopDamage();
                TopDamage = attackerInfos.FirstOrDefault(p => p.Attacker == topDamage);
                attackerInfos.Remove(FinalBlow); 
                attackerInfos.Remove(TopDamage);
            }
            AttackerInfos = attackerInfos;
        }

        private List<CargoItemInfo> InitCargoItemInfos()
        {
            static List<int> findIds(CargoItem item)
            {
                List<int> ids = new List<int>
                {
                    item.ItemTypeId
                };
                if (item.Items.NotNullOrEmpty())
                {
                    foreach (var subItem in item.Items)
                    {
                        ids.AddRange(findIds(subItem));
                    }
                }
                return ids;
            }
            List<int> cargoIds = new List<int>();
            foreach (var item in KBItemInfo.SKBDetail.Victim.Items)
            {
                cargoIds.AddRange(findIds(item));
            }
            var cargoTypes = Core.Services.DB.InvTypeService.QueryTypes(cargoIds);
            if (cargoTypes.NotNullOrEmpty())
            {
                var cargoTypesDic = cargoTypes.ToDictionary(p => p.TypeID);
                CargoItemInfo createCargoItemInfo(CargoItem item)
                {
                    CargoItemInfo cargoItemInfo = new CargoItemInfo(item);
                    if (cargoTypesDic.TryGetValue(item.ItemTypeId, out var type))
                    {
                        cargoItemInfo.Type = type;
                    }
                    if (item.Items.NotNullOrEmpty())
                    {
                        cargoItemInfo.SubItems = new List<CargoItemInfo>();
                        foreach (var subItem in item.Items)
                        {
                            cargoItemInfo.SubItems.Add(createCargoItemInfo(subItem));
                        }
                    }
                    return cargoItemInfo;
                }
                List<CargoItemInfo> cargoItemInfos = new List<CargoItemInfo>();
                foreach (var item in KBItemInfo.SKBDetail.Victim.Items)
                {
                    cargoItemInfos.Add(createCargoItemInfo(item));
                }
                return cargoItemInfos;
            }
            return null;
        }

        private async Task<List<AttackerInfo>> InitAttackerInfosAsync()
        {
            var characterIds = KBItemInfo.SKBDetail.Attackers.Select(p=>p.CharacterId).ToList();
            var corpIds = KBItemInfo.SKBDetail.Attackers.Select(p => p.CorporationId).ToList();
            var allianceIds = KBItemInfo.SKBDetail.Attackers.Select(p => p.AllianceId).ToList();
            List<int> ids = characterIds.ToList();
            ids.AddRange(corpIds);
            ids.AddRange(allianceIds);
            ids = ids.Distinct().ToList();
            ids.Remove(0);
            var idNames = await Core.Services.IDNameService.GetByIdsAsync(ids);
            var idNamesDic = idNames.ToDictionary(p => p.Id);

            var shipTypeIds = KBItemInfo.SKBDetail.Attackers.Select(p => p.ShipTypeId).ToList();
            var weaponIds = KBItemInfo.SKBDetail.Attackers.Select(p => p.WeaponTypeId).ToList();
            var typeIds = shipTypeIds.ToList();
            typeIds.AddRange(weaponIds);
            typeIds = typeIds.Distinct().ToList();
            typeIds.Remove(0);
            var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(typeIds.Distinct().ToList());
            var typesDic = types.ToDictionary(p => p.TypeID);
            List<AttackerInfo> infos = new List<AttackerInfo>();
            foreach (var attacker in KBItemInfo.SKBDetail.Attackers)
            {
                AttackerInfo attackerInfo = new AttackerInfo(attacker);
                attackerInfo.DamageRatio = (float)attacker.DamageDone / KBItemInfo.TotalDamage;
                if (attackerInfo.Attacker.CharacterId > 0)
                {
                    if (idNamesDic.TryGetValue(attackerInfo.Attacker.CharacterId, out var idName))
                    {
                        attackerInfo.CharacterName = idName;
                    }
                }
                if (attackerInfo.Attacker.CorporationId > 0)
                {
                    if (idNamesDic.TryGetValue(attackerInfo.Attacker.CorporationId, out var idName))
                    {
                        attackerInfo.CorpName = idName;
                    }
                }
                if (attackerInfo.Attacker.AllianceId > 0)
                {
                    if (idNamesDic.TryGetValue(attackerInfo.Attacker.AllianceId, out var idName))
                    {
                        attackerInfo.AllianceName = idName;
                    }
                }

                if(typesDic.TryGetValue(attacker.ShipTypeId, out var shipType))
                {
                    attackerInfo.Ship = shipType;
                }

                if (typesDic.TryGetValue(attacker.WeaponTypeId, out var weaponType))
                {
                    attackerInfo.Weapon = weaponType;
                }

                infos.Add(attackerInfo);
            }
            return infos;
        }

        public ICommand CheckTypeCommand => new RelayCommand<int>((id) =>
        {

        });
        public ICommand CheckGroupCommand => new RelayCommand<int>((id) =>
        {

        });
        public ICommand CheckSystemCommand => new RelayCommand<int>((id) =>
        {

        });
        public ICommand CheckRegionCommand => new RelayCommand<int>((id) =>
        {

        });
        public ICommand CopyLinkCommand => new RelayCommand(() =>
        {
            Windows.ApplicationModel.DataTransfer.DataPackage dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            dataPackage.SetText($"https://zkillboard.com/kill/{KBItemInfo.SKBDetail.KillmailId}/");
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            Window?.ShowSuccess("已复制");
        });
        public ICommand OpenInBrowerCommand => new RelayCommand(() =>
        {
            Helpers.UrlHelper.OpenInBrower($"https://zkillboard.com/kill/{KBItemInfo.SKBDetail.KillmailId}/");
        });
    }
}
