using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    public class ContractInfoHelper
    {
        public static async Task CompleteinfoAsync(List<Core.Models.Contract.ContractInfo> datas)
        {
            #region name
            List<long> nameIds = new List<long>();
            var list = datas.Select(p => p.IssuerId).ToList();
            if (list.NotNullOrEmpty())
            {
                nameIds.AddRange(list);
            }
            list = datas.Select(p => p.AssigneeId).ToList();
            if (list.NotNullOrEmpty())
            {
                nameIds.AddRange(list);
            }
            list = datas.Select(p => p.AcceptorId).ToList();
            if (list.NotNullOrEmpty())
            {
                nameIds.AddRange(list);
            }
            var nameResp = await Core.Services.ESIService.Current.EsiClient.Universe.GetNamesAndCategoriesFromIdsAsync(nameIds.Distinct().Select(p => (long)p).ToList());
            if (nameResp != null)
            {
                if (nameResp.Model != null)
                {
                    var namesDic = nameResp.Model.ToDictionary(p => p.Id);
                    foreach (var data in datas)
                    {
                        if (namesDic.TryGetValue(data.IssuerId, out var name))
                        {
                            data.IssuerName = name.Name;
                        }
                        else
                        {
                            data.IssuerName = data.IssuerId.ToString();
                        }
                        if (data.AssigneeId > 0)
                        {
                            if (namesDic.TryGetValue(data.AssigneeId, out var name2))
                            {
                                data.AssigneeName = name2.Name;
                            }
                            else
                            {
                                data.AssigneeName = data.AssigneeId.ToString();
                            }
                        }
                        if (data.AcceptorId > 0)
                        {
                            if (namesDic.TryGetValue(data.AcceptorId, out var name2))
                            {
                                data.AcceptorName = name2.Name;
                            }
                            else
                            {
                                data.AcceptorName = data.AcceptorId.ToString();
                            }
                        }
                    }
                }
                else
                {
                    Log.Error("GetNamesAndCategoriesFromIdsAsync Failed");
                }
            }
            #endregion
            #region LocationName
            HashSet<long> allLocationIds = new HashSet<long>();
            foreach (var data in datas)
            {
                if (data.StartLocationId > 0)
                {
                    allLocationIds.Add(data.StartLocationId.Value);
                }
                if (data.EndLocationId > 0)
                {
                    allLocationIds.Add(data.EndLocationId.Value);
                }
            }
            if (allLocationIds.Count > 0)
            {
                var stations = allLocationIds.Where(p => p > 70000000).ToList();
                var structures = allLocationIds.Except(stations).ToList();
                Dictionary<long, string> locationNames = new Dictionary<long, string>();
                if (stations.NotNullOrEmpty())
                {
                    var staStaions = await Core.Services.DB.StaStationService.QueryAsync(stations);
                    if (staStaions.NotNullOrEmpty())
                    {
                        foreach (var staSta in staStaions)
                        {
                            locationNames.Add(staSta.StationID, staSta.StationName);
                        }
                    }
                }
                if (structures.NotNullOrEmpty())
                {
                    var structuresResp = await Core.Services.ESIService.Current.EsiClient.Universe.GetNamesAndCategoriesFromIdsAsync(structures.Select(p => (long)p).ToList());
                    if (structuresResp.Model != null)
                    {
                        foreach (var data in structuresResp.Model)
                        {
                            locationNames.Add(data.Id, data.Name);
                        }
                    }
                    else
                    {
                        Log.Error("GetNamesAndCategoriesFromIdsAsync Failed");
                    }
                }
                foreach (var data in datas)
                {
                    if (data.StartLocationId > 0)
                    {
                        if (locationNames.TryGetValue(data.StartLocationId.Value, out var value))
                        {
                            data.StartLocationName = value;
                        }
                        else
                        {
                            data.StartLocationName = data.StartLocationId.ToString();
                        }
                    }
                    if (data.EndLocationId > 0)
                    {
                        if (locationNames.TryGetValue(data.EndLocationId.Value, out var value))
                        {
                            data.EndLocationName = value;
                        }
                        else
                        {
                            data.EndLocationName = data.EndLocationId.ToString();
                        }
                    }
                }
            }
            #endregion

            #region ResouceName
            string unknown = Helpers.ResourcesHelper.GetString("ContractPage_Type_Unknown");
            string itemExchange = Helpers.ResourcesHelper.GetString("ContractPage_Type_ItemExchange");
            string auction = Helpers.ResourcesHelper.GetString("ContractPage_Type_Auction");
            string courier = Helpers.ResourcesHelper.GetString("ContractPage_Type_Courier");
            string loan = Helpers.ResourcesHelper.GetString("ContractPage_Type_Loan");
            foreach (var data in datas)
            {
                switch(data.GetTypeEnum())
                {
                    case Core.Models.Contract.ContractInfo.TypeEnum.unknown:data.TypeStr = unknown;break;
                    case Core.Models.Contract.ContractInfo.TypeEnum.item_exchange: data.TypeStr = itemExchange; break;
                    case Core.Models.Contract.ContractInfo.TypeEnum.auction: data.TypeStr = auction; break;
                    case Core.Models.Contract.ContractInfo.TypeEnum.courier: data.TypeStr = courier; break;
                    case Core.Models.Contract.ContractInfo.TypeEnum.loan: data.TypeStr = loan; break;
                }
            }
            #endregion
        }
    }
}
