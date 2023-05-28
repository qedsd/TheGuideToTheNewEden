using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Wins;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ContractViewModel: BaseViewModel
    {
        private int page = 1;
        public int Page
        {
            get => page;
            set
            {
                SetProperty(ref page, value);
                GetAllContracts();
            }
        }
        private int contractType = 0;
        /// <summary>
        /// 0为所有
        /// 1-5依次为ESI.NET.Enumerations.ContractType
        /// </summary>
        public int ContractType
        {
            get => contractType;
            set
            {
                SetProperty(ref contractType, value);
                GetContracts();
            }
        }
        private List<Core.Models.Contract.ContractInfo> allContracts;
        /// <summary>
        /// 当前星系当前页所有
        /// </summary>
        public List<Core.Models.Contract.ContractInfo> AllContracts
        {
            get => allContracts;
            set
            {
                SetProperty(ref allContracts, value);
                GetContracts();
            }
        }
        private List<Core.Models.Contract.ContractInfo> contracts;
        /// <summary>
        /// 当前星系当前页筛选合同类型后显示的
        /// </summary>
        public List<Core.Models.Contract.ContractInfo> Contracts
        {
            get => contracts;
            set
            {
                SetProperty(ref contracts, value);
            }
        }
        private List<Core.DBModels.MapRegionBase> mapRegions;
        public List<Core.DBModels.MapRegionBase> MapRegions
        {
            get => mapRegions;
            set => SetProperty(ref mapRegions, value);
        }
        private Core.DBModels.MapRegionBase selectedMapRegionBase;
        public Core.DBModels.MapRegionBase SelectedMapRegionBase
        {
            get => selectedMapRegionBase;
            set
            {
                SetProperty(ref selectedMapRegionBase, value);
                GetAllContracts();
            }
        }

        private Core.Models.Contract.ContractInfo selectedContractInfo;
        public Core.Models.Contract.ContractInfo SelectedContractInfo
        {
            get => selectedContractInfo;
            set
            {
                SetProperty(ref selectedContractInfo, value);
                if(value!= null)
                {
                    LoadDetail(value);
                }
            }
        }

        public ContractViewModel() 
        {
            
        }
        public void Init()
        {
            InitSolarSystems();
            SelectedMapRegionBase = MapRegions.FirstOrDefault(p => p.RegionID == 10000002);
        }
        private async void InitSolarSystems()
        {
            var list = await Core.Services.DB.MapRegionService.QueryAllAsync();
            if (list.NotNullOrEmpty())
            {
                MapRegions = list.Select(p => p as Core.DBModels.MapRegionBase).ToList();
            }
        }
        private async void GetAllContracts()
        {
            if(Page <= 0 || SelectedMapRegionBase == null)
            {
                return;
            }
            Window?.ShowWaiting();
            var resp = await Core.Services.ESIService.Current.EsiClient.Contracts.Contracts(SelectedMapRegionBase.RegionID, Page);
            if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var datas = resp.Data.Select(p=>new Core.Models.Contract.ContractInfo(p)).ToList();
                if(datas.NotNullOrEmpty())
                {
                    await ContractInfoHelper.CompleteinfoAsync(datas);
                }
                AllContracts = datas;
            }
            else
            {
                AllContracts = null;
                Core.Log.Error(resp?.Message);
                Window?.ShowError(resp?.Message, true);
            }
            Window?.HideWaiting();
        }
        private void GetContracts()
        {
            if(AllContracts.NotNullOrEmpty())
            {
                if(ContractType == 0)
                {
                    Contracts = AllContracts.ToList();
                }
                else
                {
                    var type = (ESI.NET.Enumerations.ContractType)(ContractType - 1);
                    Contracts = AllContracts.Where(p => p.Type == type).ToList();
                }
            }
            else
            {
                Contracts = null;
            }
        }

        private void LoadDetail(Core.Models.Contract.ContractInfo contractInfo)
        {
            new ContractDetailWindow(Core.Services.ESIService.Current.EsiClient, contractInfo, 0).Activate();
        }
    }
}
