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
            List<int> nameIds = new List<int>();
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
            var nameResp = await Core.Services.ESIService.Current.EsiClient.Universe.Names(nameIds.Distinct().ToList());
            if (nameResp != null)
            {
                if (nameResp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var namesDic = nameResp.Data.ToDictionary(p => p.Id);
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
                    Log.Error(nameResp.Message);
                }
            }
            #endregion
            #region LocationName
            HashSet<long> allLocationIds = new HashSet<long>();
            foreach (var data in datas)
            {
                if (data.StartLocationId > 0)
                {
                    allLocationIds.Add(data.StartLocationId);
                }
                if (data.EndLocationId > 0)
                {
                    allLocationIds.Add(data.EndLocationId);
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
                    var structuresResp = await Core.Services.ESIService.Current.EsiClient.Universe.Names(structures.Select(p => (int)p).ToList());
                    if (structuresResp != null && structuresResp.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        foreach (var data in structuresResp.Data)
                        {
                            locationNames.Add(data.Id, data.Name);
                        }
                    }
                    else
                    {
                        Log.Error(structuresResp?.Message);
                    }
                }
                foreach (var data in datas)
                {
                    if (data.StartLocationId > 0)
                    {
                        if (locationNames.TryGetValue(data.StartLocationId, out var value))
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
                        if (locationNames.TryGetValue(data.EndLocationId, out var value))
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
                switch(data.Type)
                {
                    case ESI.NET.Enumerations.ContractType.Unknown:data.TypeStr = unknown;break;
                    case ESI.NET.Enumerations.ContractType.ItemExchange: data.TypeStr = itemExchange; break;
                    case ESI.NET.Enumerations.ContractType.Auction: data.TypeStr = auction; break;
                    case ESI.NET.Enumerations.ContractType.Courier: data.TypeStr = courier; break;
                    case ESI.NET.Enumerations.ContractType.Loan: data.TypeStr = loan; break;
                }
            }
            #endregion
        }
    }
}
