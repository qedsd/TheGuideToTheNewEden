using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services
{
    /// <summary>
    /// 按 ID 解析名称：优先查本地库，未命中再走 ESI 的 /universe/names。
    /// </summary>
    /// <remarks>
    /// 本服务的 ID 统一为 <see cref="int"/>（<see cref="DBModels.IdName.Id"/> 即为 int），
    /// 因此只适用于军团 / 角色 / 联盟 / 星系 / 空间站 / 物品类型等**处于 int 范围内**的 ID。
    /// 结构（structure）的 ID 约 1e12，远超 <see cref="int.MaxValue"/>，
    /// 传入会被截断成错误的值 —— **结构的名称解析请改用各 UI 项目中的 StructureService**
    /// （Core 内不提供结构名称解析）。
    /// </remarks>
    public class IDNameService
    {
        #region 保存到数据库
        private static Task _saveThread;
        private static ConcurrentQueue<DBModels.IdName> _saveQueue;
        /// <summary>
        /// 用于暂存当前需要保存到数据库的数据
        /// </summary>
        private static Dictionary<int, IdName> _tempDic;
        private static void SaveToDB(List<DBModels.IdName> idNames)
        {
            _saveQueue ??= new ConcurrentQueue<IdName>();
            _tempDic ??= new Dictionary<int, IdName>();
            foreach (var  idName in idNames)
            {
                _saveQueue.Enqueue(idName);
            }
            if(_saveThread == null)
            {
                _saveThread = new Task(() =>
                {
                    while (true)
                    {
                        try
                        {
                            _tempDic.Clear();
                            Dictionary<int, IdName> dic = new Dictionary<int, IdName>();
                            IdName name = null;
                            while (_saveQueue.TryDequeue(out name))
                            {
                                dic.TryAdd(name.Id, name);
                            }
                            IDNameDBService.Insert(dic.Values.ToList());
                        }
                        catch (Exception)
                        {
                            //Core.Log.Error(ex);
                        }
                        Thread.Sleep(1000);//一秒钟检查一次是否有插入
                    }
                });
                _saveThread.Start();
            }
        }
        #endregion
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
        public static DBModels.IdName GetById(int id)
        {
            var ids = GetByIds(new List<int>() { id });
            if (ids?.Count > 0)
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
        /// <summary>
        /// 按 ID 解析名称（long 重载，异步）。
        /// </summary>
        /// <remarks>
        /// 与同步的 <see cref="GetByIds(List{long})"/> 行为一致：内部转换为 int，
        /// 超出 <see cref="int.MaxValue"/> 的 ID 会被截断；结构 ID 请改用 StructureService。
        /// </remarks>
        public static async Task<List<DBModels.IdName>> GetByIdsAsync(List<long> ids)
        {
            return await Task.Run(() => GetByIds(ids));
        }
        /// <summary>
        /// 按 ID 解析名称（long 重载）。
        /// </summary>
        /// <remarks>
        /// 内部会转换为 <see cref="int"/>，超出 <see cref="int.MaxValue"/> 的 ID 会被**截断**，
        /// 从而静默解析出错误的名字（而不是报错）。
        /// 结构（structure）的 ID 请改用 StructureService。
        /// </remarks>
        public static List<DBModels.IdName> GetByIds(List<long> ids)
        {
            return GetByIds(ids.Select(p => (int)p).ToList());
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
                List<long> noInDbs;
                List<DBModels.IdName> inDbResults = IDNameDBService.Query(ids);
                if (inDbResults.NotNullOrEmpty())
                {
                    noInDbs = ids.Except(inDbResults.Select(p => p.Id)).Select(p=>(long)p).ToList();
                }
                else
                {
                    noInDbs = ids.Select(p => (long)p).ToList();
                }

                //2.查找数据库不存在的
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                if (noInDbs.Count > 0)
                {
                    var resp = ESIService.Current.EsiClient.Universe.GetNamesAndCategoriesFromIdsAsync(noInDbs).Result;
                    if (resp.Model != null)
                    {
                        foreach (var data in resp.Model)
                        {
                            noInDbResults.Add(new DBModels.IdName((int)data.Id, data.Name, data.Category));
                        }
                    }
                }
                //TODO:处理查找不到的

                //3.保存数据库不存在的
                SaveToDB(noInDbResults);

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
                    noInDbs = names.Except(inDbResults.Select(p => p.Name)).ToList();
                }
                else
                {
                    noInDbs = names;
                }

                //2.查找数据库不存在的
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                void AddData(List<EVEStandard.Models.NameToId> resolvedInfos, Core.DBModels.IdName.CategoryEnum category)
                {
                    if (resolvedInfos.NotNullOrEmpty())
                    {
                        foreach (var data in resolvedInfos)
                        {
                            noInDbResults.Add(new DBModels.IdName(data.Id, data.Name, (int)category));
                        }
                    }
                }
                int start = 0;
                int length = noInDbs.Count > 500 ? 500 : noInDbs.Count;
                while(true)
                {
                    var resp = await ESIService.Current.EsiClient.Universe.BulkNamesToIdsAsync(noInDbs.Skip(start).Take(length).ToList());
                    if (resp.Model != null)
                    {
                        AddData(resp.Model.Alliances, DBModels.IdName.CategoryEnum.Alliance);
                        AddData(resp.Model.Characters, DBModels.IdName.CategoryEnum.Character);
                        AddData(resp.Model.Constellations, DBModels.IdName.CategoryEnum.Constellation);
                        AddData(resp.Model.Corporations, DBModels.IdName.CategoryEnum.Corporation);
                        AddData(resp.Model.InventoryTypes, DBModels.IdName.CategoryEnum.InventoryType);
                        AddData(resp.Model.Regions, DBModels.IdName.CategoryEnum.Region);
                        AddData(resp.Model.Systems, DBModels.IdName.CategoryEnum.SolarSystem);
                        AddData(resp.Model.Stations, DBModels.IdName.CategoryEnum.Station);
                        AddData(resp.Model.Factions, DBModels.IdName.CategoryEnum.Faction);
                        int found = start + length;
                        int remain = noInDbs.Count - found;
                        if (remain > 0)
                        {
                            start += length;
                            length = remain > 500 ? 500 : remain;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                

                //3.保存数据库不存在的
                SaveToDB(noInDbResults);

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

        public static async Task<List<DBModels.IdName>> SerachByNameAsync(string name)
        {
            try
            {
                List<DBModels.IdName> results = await IDNameDBService.SearchAsync(name);
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                void AddData(List<EVEStandard.Models.NameToId> resolvedInfos, Core.DBModels.IdName.CategoryEnum category)
                {
                    if (resolvedInfos.NotNullOrEmpty())
                    {
                        foreach (var data in resolvedInfos)
                        {
                            var idName = new DBModels.IdName(data.Id, data.Name, category);
                            results.Add(idName);
                            noInDbResults.Add(idName);
                        }
                    }
                }
                var resp = await ESIService.Current.EsiClient.Universe.BulkNamesToIdsAsync(new List<string>() { name});
                if (resp.Model != null)
                {
                    AddData(resp.Model.Alliances, DBModels.IdName.CategoryEnum.Alliance);
                    AddData(resp.Model.Characters, DBModels.IdName.CategoryEnum.Character);
                    AddData(resp.Model.Constellations, DBModels.IdName.CategoryEnum.Constellation);
                    AddData(resp.Model.Corporations, DBModels.IdName.CategoryEnum.Corporation);
                    AddData(resp.Model.InventoryTypes, DBModels.IdName.CategoryEnum.InventoryType);
                    AddData(resp.Model.Regions, DBModels.IdName.CategoryEnum.Region);
                    AddData(resp.Model.Systems, DBModels.IdName.CategoryEnum.SolarSystem);
                    AddData(resp.Model.Stations, DBModels.IdName.CategoryEnum.Station);
                    AddData(resp.Model.Factions, DBModels.IdName.CategoryEnum.Faction);
                }
                if(noInDbResults.Any())
                {
                    SaveToDB(noInDbResults);
                }
                return results;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return null;
        }

        public static List<DBModels.IdName> SerachByName(string name)
        {
            try
            {
                List<DBModels.IdName> results = IDNameDBService.Search(name);
                List<DBModels.IdName> noInDbResults = new List<DBModels.IdName>();
                void AddData(List<EVEStandard.Models.NameToId> resolvedInfos, Core.DBModels.IdName.CategoryEnum category)
                {
                    if (resolvedInfos.NotNullOrEmpty())
                    {
                        foreach (var data in resolvedInfos)
                        {
                            var idName = new DBModels.IdName(data.Id, data.Name, category);
                            results.Add(idName);
                            noInDbResults.Add(idName);
                        }
                    }
                }
                var resp = ESIService.Current.EsiClient.Universe.BulkNamesToIdsAsync(new List<string>() { name }).Result;
                if (resp.Model != null)
                {
                    AddData(resp.Model.Alliances, DBModels.IdName.CategoryEnum.Alliance);
                    AddData(resp.Model.Characters, DBModels.IdName.CategoryEnum.Character);
                    AddData(resp.Model.Constellations, DBModels.IdName.CategoryEnum.Constellation);
                    AddData(resp.Model.Corporations, DBModels.IdName.CategoryEnum.Corporation);
                    AddData(resp.Model.InventoryTypes, DBModels.IdName.CategoryEnum.InventoryType);
                    AddData(resp.Model.Regions, DBModels.IdName.CategoryEnum.Region);
                    AddData(resp.Model.Systems, DBModels.IdName.CategoryEnum.SolarSystem);
                    AddData(resp.Model.Stations, DBModels.IdName.CategoryEnum.Station);
                    AddData(resp.Model.Factions, DBModels.IdName.CategoryEnum.Faction);
                }
                if (noInDbResults.Any())
                {
                    SaveToDB(noInDbResults);
                }
                return results;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return null;
        }
    }
}
