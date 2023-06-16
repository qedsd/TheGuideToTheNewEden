using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Models.Market;
using static TheGuideToTheNewEden.Core.Models.Market.ScalperSetting;
using TheGuideToTheNewEden.Core.Extensions;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Business
{
    public class ScalperViewModel:BaseViewModel
    {
        private ScalperSetting setting;
        public ScalperSetting Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }

        private int buyPriceType;
        public int BuyPriceType
        {
            get => buyPriceType;
            set
            {
                SetProperty(ref buyPriceType, value);
                Setting.BuyPrice = (PriceType)value;
            }
        }

        private int sellPriceType;
        public int SellPriceType
        {
            get => sellPriceType;
            set
            {
                SetProperty(ref sellPriceType, value);
                Setting.SellPrice = (PriceType)value;
            }
        }

        private List<Core.DBModels.InvMarketGroup> invMarketGroups;
        public List<Core.DBModels.InvMarketGroup> InvMarketGroups
        {
            get => invMarketGroups;
            set
            {
                SetProperty(ref invMarketGroups, value);
            }
        }

        private Core.DBModels.InvMarketGroup selectedInvMarketGroup;
        public Core.DBModels.InvMarketGroup SelectedInvMarketGroup
        {
            get => selectedInvMarketGroup;
            set
            {
                SetProperty(ref selectedInvMarketGroup, value);
            }
        }

        public ScalperViewModel()
        {
            Init();
        }
        private static readonly string SettingFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ScalperSetting.json");
        private static readonly string SettingFolderPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        private async void Init()
        {
            if(System.IO.File.Exists(SettingFilePath))
            {
                string json = System.IO.File.ReadAllText(SettingFilePath);
                if(!string.IsNullOrEmpty(json))
                {
                    Setting = JsonConvert.DeserializeObject<ScalperSetting>(json);
                }
            }
            Setting ??= new ScalperSetting();
            InvMarketGroups = await Core.Services.DB.InvMarketGroupService.QueryRootGroupAsync();
            SelectedInvMarketGroup = InvMarketGroups.FirstOrDefault(p => p.MarketGroupID == Setting.MarketGroup);
        }

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            Window?.ShowWaiting();
            if(IsValid())
            {
                SaveSetting();
                await Start();
            }
            Window?.HideWaiting();
        });
        private void SaveSetting()
        {
            if (!System.IO.Directory.Exists(SettingFolderPath))
            {
                System.IO.Directory.CreateDirectory(SettingFolderPath);
            }
            string json = JsonConvert.SerializeObject(Setting);
            System.IO.File.WriteAllText(SettingFilePath, json);
        }


        private bool IsValid()
        {
            if(Setting.SourceMarketLocation == null)
            {
                Window?.ShowError("请选择源市场");
                return false;
            }
            if(Setting.DestinationMarketLocation == null)
            {
                Window?.ShowError("请选择目的市场");
                return false;
            }
            if(SelectedInvMarketGroup == null)
            {
                Window?.ShowError("请选择物品类型");
                return false;
            }
            return true;
        }

        private async Task<List<Core.Models.Market.Order>> GetAllSourceOrders()
        {
            List<Core.Models.Market.Order> orders = null;
            switch(Setting.SourceMarketLocation.Type)
            {
                case MarketLocationType.Region:
                    {
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.SourceMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.SourceMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.SourceMarketLocation.Id);
                    }
                    break;
            }
            return orders;
        }
        private async Task<List<Core.Models.Market.Order>> GetAllDestinationOrders()
        {
            List<Core.Models.Market.Order> orders = null;
            switch (Setting.DestinationMarketLocation.Type)
            {
                case MarketLocationType.Region:
                    {
                        orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync((int)Setting.DestinationMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.SolarSystem:
                    {
                        orders = await Services.MarketOrderService.Current.GetMapSolarSystemOrdersAsync((int)Setting.DestinationMarketLocation.Id);
                    }
                    break;
                case MarketLocationType.Structure:
                    {
                        orders = await Services.MarketOrderService.Current.GetStructureOrdersAsync(Setting.DestinationMarketLocation.Id);
                    }
                    break;
            }
            return orders;
        }
        private async Task Start()
        {
            var allSourceOrders = await GetAllSourceOrders();
            var allDestinationOrders = await GetAllDestinationOrders();
            
            if(allSourceOrders.NotNullOrEmpty() && allDestinationOrders.NotNullOrEmpty())
            {
                var groups = await GetTypeIdsInGroup();
                List<int> sourceTypeIds = new List<int>();
                List<int> destinationTypeIds = new List<int>();
                await Task.Run(() =>
                {
                    var subGroup = groups.Select(p => p.MarketGroupID).ToHashSet2();
                    foreach (var order in allSourceOrders.Where(p => p.InvType != null && p.InvType.MarketGroupID != null))
                    {
                        if (subGroup.Contains((int)order.InvType.MarketGroupID))
                        {
                            sourceTypeIds.Add((int)order.TypeId);
                        }
                    }
                    foreach (var order in allDestinationOrders.Where(p => p.InvType != null && p.InvType.MarketGroupID != null))
                    {
                        if (subGroup.Contains((int)order.InvType.MarketGroupID))
                        {
                            destinationTypeIds.Add((int)order.TypeId);
                        }
                    }
                });
                
                var sourceHistory = await Services.MarketOrderService.Current.GetHistoryAsync(sourceTypeIds.Distinct().ToList(), Setting.SourceMarketLocation.RegionId);
                var destinationHistory = await Services.MarketOrderService.Current.GetHistoryAsync(destinationTypeIds.Distinct().ToList(), Setting.DestinationMarketLocation.RegionId);
            }
            
        }

        private async Task<List<Core.DBModels.InvMarketGroup>> GetTypeIdsInGroup()
        {
            var subGroups = await Core.Services.DB.InvMarketGroupService.QuerySubGroupAsync();
            return await Task.Run(() =>
            {
                List<Core.DBModels.InvMarketGroup> targetroups = new List<Core.DBModels.InvMarketGroup>();
                foreach (var group in subGroups)
                {
                    var top = GetTopGroup(subGroups, group);
                    if (top.ParentGroupID == SelectedInvMarketGroup.MarketGroupID)
                    {
                        targetroups.Add(group);
                    }
                }
                return targetroups;
            });
        }
        private Core.DBModels.InvMarketGroup GetTopGroup(List<Core.DBModels.InvMarketGroup> subGroups, Core.DBModels.InvMarketGroup targetGroup)
        {
            var parent = subGroups.FirstOrDefault(p=>p.MarketGroupID == targetGroup.ParentGroupID);
            if(parent == null)
            {
                return targetGroup;
            }
            else
            {
                return GetTopGroup(subGroups, parent);
            }
        }
    }
}
