using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvTypeService
    {
        public static async Task<InvType> QueryTypeAsync(long id)
        {
            var type = await DBService.MainDb.Queryable<InvType>().FirstAsync(p => p.TypeID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypeAsync(type);
            }
            return type;
        }
        public static InvType QueryType(long id, bool local = true)
        {
            var type = DBService.MainDb.Queryable<InvType>().First(p => p.TypeID == id);
            if (local && DBService.NeedLocalization)
            {
                LocalDbService.TranInvType(type);
            }
            return type;
        }
        public static async Task<List<InvType>> QueryTypesAsync(List<long> ids)
        {
            var types = await DBService.MainDb.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
            }
            return types;
        }
        public static async Task<List<InvType>> QueryTypesAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
            }
            return types;
        }
        public static List<InvType> QueryTypes(List<int> ids)
        {
            var types = DBService.MainDb.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvTypes(types);
            }
            return types;
        }

        public static async Task<List<InvType>> QueryTypesInGroupAsync(int groupId)
        {
            var types = await DBService.MainDb.Queryable<InvType>().Where(p => p.GroupID == groupId).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
            }
            return types;
        }

        /// <summary>
        /// 按 ID 批量查舰船质量（虫洞过洞计算用）。
        /// Mass 列只有 <see cref="InvTypeMass"/> 映射，正常 <see cref="InvType"/> 查询拿不到。
        /// </summary>
        public static async Task<List<InvTypeMass>> QueryTypeMassAsync(List<int> typeIds)
        {
            if (typeIds == null || typeIds.Count == 0)
            {
                return new List<InvTypeMass>();
            }
            return await DBService.MainDb.Queryable<InvTypeMass>().Where(p => typeIds.Contains(p.TypeID)).ToListAsync();
        }

        /// <summary>
        /// 查询全部玩家可用舰船的类型与质量（过洞计算器"选择具体舰船"用）：
        /// groups 表取 CategoryID=6（舰船）的组，再从 types 表按组取 Mass；
        /// MarketGroupID 非空排除 NPC 专属船，名称按需本地化。
        /// </summary>
        public static async Task<List<InvTypeMass>> QueryShipMassAsync()
        {
            var shipGroupIds = DBService.MainDb.Queryable<InvGroup>().Where(p => p.CategoryID == 6).Select(p => p.GroupID).ToList();
            if (shipGroupIds == null || shipGroupIds.Count == 0)
            {
                return new List<InvTypeMass>();
            }

            var types = await DBService.MainDb.Queryable<InvTypeMass>()
                .Where(p => shipGroupIds.Contains(p.GroupID) && p.Mass > 0 && p.MarketGroupID != null)
                .ToListAsync();
            if (types == null || types.Count == 0)
            {
                return new List<InvTypeMass>();
            }

            if (DBService.NeedLocalization)
            {
                var localized = await LocalDbService.TranInvTypesAsync(types.Select(p => p.TypeID).ToList());
                if (localized != null && localized.Count > 0)
                {
                    var nameById = new Dictionary<int, string>();
                    foreach (var item in localized)
                    {
                        if (!string.IsNullOrEmpty(item.TypeName))
                        {
                            nameById[item.TypeID] = item.TypeName;
                        }
                    }
                    foreach (var type in types)
                    {
                        if (nameById.TryGetValue(type.TypeID, out var name))
                        {
                            type.TypeName = name;
                        }
                    }
                }
            }
            return types;
        }

        public static List<InvType> QueryTypesInGroup(List<int> groupIds)
        {
            var types = DBService.MainDb.Queryable<InvType>().Where(p => p.MarketGroupID != null && groupIds.Contains((int)p.MarketGroupID)).ToList();
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvTypes(types);
            }
            return types;
        }
        public static List<InvTypeBase> QueryTypesOfGroupMainAndLocal(List<int> groupIds)
        {
            var groupIdsH = groupIds.ToHashSet2();
            var mainTypes = DBService.MainDb.Queryable<InvType>().Where(p => p.MarketGroupID != null && groupIdsH.Contains(p.GroupID)).ToList();
            if (mainTypes.NotNullOrEmpty())
            {
                var typeIds = mainTypes.Select(p => p.TypeID).ToHashSet2();
                List<InvTypeBase> allTypes = mainTypes.Select(p => p as InvTypeBase).ToList();
                var localTypes = DBService.LocalDb?.Queryable<InvTypeBase>().Where(p=> typeIds.Contains(p.TypeID)).ToList();
                if (localTypes.NotNullOrEmpty())
                {
                    allTypes.AddRange(localTypes);
                }
                return allTypes;
            }
            else
            {
                return null;
            }
        }

        public static async Task<List<InvType>> QueryMarketTypesAsync()
        {
            var types = await DBService.MainDb.Queryable<InvType>().Where(p => p.MarketGroupID != null).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
            }
            return types;
        }
        public static async Task<List<InvType>> QueryByNameAsync(string name, bool isLike = true)
        {
            List<InvType> types;
            if(isLike)
            {
                types = await DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(name)).ToListAsync();
            }
            else
            {
                types = await DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Equals(name)).ToListAsync();
            }
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
            }
            return types;
        }
        public static List<InvType> QueryByName(string name, bool isLike = true)
        {
            List<InvType> types;
            if (isLike)
            {
                types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(name)).ToList();
            }
            else
            {
                types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Equals(name)).ToList();
            }
            if (DBService.NeedLocalization)
            {
                LocalDbService.TranInvTypes(types);
            }
            return types;
        }

        /// <summary>
        /// 模糊搜索物品名，支持本地化数据库
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        public static async Task<List<DataBaseSearchItem>> SearchAsync(string partName)
        {
            return await Task.Run(() =>
            {
                List<DataBaseSearchItem> searchInvTypes = new List<DataBaseSearchItem>();
                var types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(partName)).ToList();
                if(types.NotNullOrEmpty())
                {
                    types.ForEach(p => searchInvTypes.Add(new DataBaseSearchItem(p)));
                }
                var localTypes = LocalDbService.SearchInvType(partName);
                if (localTypes.NotNullOrEmpty())
                {
                    localTypes.ForEach(p => searchInvTypes.Add(new DataBaseSearchItem(p)));
                }
                return searchInvTypes;
            });
        }
        /// <summary>
        /// 模糊搜索物品名，支持本地化数据库
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        public static List<DataBaseSearchItem> Search(string partName)
        {
            List<DataBaseSearchItem> searchInvTypes = new List<DataBaseSearchItem>();
            var types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(partName)).ToList();
            if (types.NotNullOrEmpty())
            {
                types.ForEach(p => searchInvTypes.Add(new DataBaseSearchItem(p)));
            }
            var localTypes = LocalDbService.SearchInvType(partName);
            if (localTypes.NotNullOrEmpty())
            {
                localTypes.ForEach(p => searchInvTypes.Add(new DataBaseSearchItem(p)));
            }
            return searchInvTypes;
        }

        public static InvTypeBase QueryMarkeType(string name)
        {
            var target = DBService.MainDb.Queryable<InvType>().First(p =>p.MarketGroupID != null && name.Equals(p.TypeName, StringComparison.OrdinalIgnoreCase));
            if(target == null)
            {
                var localTypes =  LocalDbService.QueryInvTypes(name);
                if (localTypes.NotNullOrEmpty())
                {
                    var localTypeIds = localTypes.Select(p=>p.TypeID).ToList();
                    var targetLocalType = DBService.MainDb.Queryable<InvType>().First(p => localTypeIds.Contains(p.TypeID) && p.MarketGroupID != null);
                    if (targetLocalType != null)
                    {
                        return localTypes.First(p => p.TypeID == targetLocalType.TypeID);
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return target;
            }
        }
    }
}
