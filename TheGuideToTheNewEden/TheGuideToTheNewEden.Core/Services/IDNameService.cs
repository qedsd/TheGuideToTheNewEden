using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services
{
    public class IDNameService
    {
        public static async Task<DBModels.IdName> GetByIdAsync(int id)
        {
            var ids = await GetByIdsAsync(new List<int>() { id});
            if(ids?.Count > 0)
            {
                return ids[0];
            }
            else
            {
                return null;
            }
        }
        public static async Task<List<DBModels.IdName>> GetByIdsAsync(List<int> ids)
        {
            return await Task.Run(() => GetByIds(ids));
        }
        public static List<DBModels.IdName> GetByIds(List<int> ids)
        {
            try
            {
                //1.优先查找数据库
                //2.查找数据库不存在的
                //3.保存数据库不存在的
                //4.合并返回

                //1.优先查找数据库
                List<int> noInDbs;
                List<DBModels.IdName> inDbResults = IDNameDBService.Query(ids);
                if (inDbResults.NotNullOrEmpty())
                {
                    noInDbs = ids.Except(inDbResults.Select(p => p.Id)).ToList();
                }
                else
                {
                    noInDbs = ids;
                }

                //2.查找数据库不存在的
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                var resp = ESIService.Current.EsiClient.Universe.Names(noInDbs).Result;
                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    foreach (var data in resp.Data)
                    {
                        noInDbResults.Add(new DBModels.IdName()
                        {
                            Id = data.Id,
                            Name = data.Name,
                            Category = (int)data.Category
                        });
                    }
                }
                //TODO:处理查找不到的

                //3.保存数据库不存在的
                IDNameDBService.Insert(noInDbResults);

                //4.合并返回
                if (inDbResults.NotNullOrEmpty())
                {
                    noInDbResults.AddRange(inDbResults);
                    return noInDbResults;
                }
                return noInDbResults;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return null;
        }
        public static async Task<DBModels.IdName> GetByName(string name)
        {
            var ids = await GetByNames(new List<string>() { name });
            if (ids?.Count > 0)
            {
                return ids[0];
            }
            else
            {
                return null;
            }
        }
        public static async Task<List<DBModels.IdName>> GetByNames(List<string> names)
        {
            try
            {
                //1.优先查找数据库
                //2.查找数据库不存在的
                //3.保存数据库不存在的
                //4.合并返回

                //1.优先查找数据库
                List<string> noInDbs;
                List<DBModels.IdName> inDbResults = await IDNameDBService.QueryAsync(names);
                if (inDbResults.NotNullOrEmpty())
                {
                    noInDbs = inDbResults.Select(p => p.Name).Except(names).ToList();
                }
                else
                {
                    noInDbs = names;
                }

                //2.查找数据库不存在的
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                void AddData(List<ESI.NET.Models.Universe.ResolvedInfo> resolvedInfos)
                {
                    if (resolvedInfos.NotNullOrEmpty())
                    {
                        foreach (var data in resolvedInfos)
                        {
                            noInDbResults.Add(new DBModels.IdName()
                            {
                                Id = data.Id,
                                Name = data.Name,
                                Category = (int)data.Category
                            });
                        }
                    }
                }
                var resp = await ESIService.Current.EsiClient.Universe.IDs(noInDbs);
                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    AddData(resp.Data.Alliances);
                    AddData(resp.Data.Characters);
                    AddData(resp.Data.Constellations);
                    AddData(resp.Data.Corporations);
                    AddData(resp.Data.InventoryTypes);
                    AddData(resp.Data.Regions);
                    AddData(resp.Data.Systems);
                    AddData(resp.Data.Stations);
                    AddData(resp.Data.Factions);
                    AddData(resp.Data.Structures);
                }

                //3.保存数据库不存在的
                await IDNameDBService.InsertAsync(noInDbResults);

                //4.合并返回
                if (inDbResults.NotNullOrEmpty())
                {
                    noInDbResults.AddRange(inDbResults);
                    return noInDbResults;
                }
                return noInDbResults;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return null;
        }
    }
}
