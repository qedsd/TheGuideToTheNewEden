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
        public static async Task<InvType> QueryTypeAsync(int id)
        {
            var type = await DBService.MainDb.Queryable<InvType>().FirstAsync(p => p.TypeID == id);
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypeAsync(type);
            }
            return type;
        }
        public static InvType QueryType(int id, bool local = true)
        {
            var type = DBService.MainDb.Queryable<InvType>().First(p => p.TypeID == id);
            if (local && DBService.NeedLocalization)
            {
                LocalDbService.TranInvType(type);
            }
            return type;
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
        public static async Task<List<TranslationSearchItem>> SearchAsync(string partName)
        {
            return await Task.Run(() =>
            {
                List<TranslationSearchItem> searchInvTypes = new List<TranslationSearchItem>();
                var types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(partName)).ToList();
                if(types.NotNullOrEmpty())
                {
                    types.ForEach(p => searchInvTypes.Add(new TranslationSearchItem(p)));
                }
                var localTypes = LocalDbService.SearchInvType(partName);
                if (localTypes.NotNullOrEmpty())
                {
                    localTypes.ForEach(p => searchInvTypes.Add(new TranslationSearchItem(p)));
                }
                return searchInvTypes;
            });
        }
        /// <summary>
        /// 模糊搜索物品名，支持本地化数据库
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        public static List<TranslationSearchItem> Search(string partName)
        {
            List<TranslationSearchItem> searchInvTypes = new List<TranslationSearchItem>();
            var types = DBService.MainDb.Queryable<InvType>().Where(p => p.TypeName.Contains(partName)).ToList();
            if (types.NotNullOrEmpty())
            {
                types.ForEach(p => searchInvTypes.Add(new TranslationSearchItem(p)));
            }
            var localTypes = LocalDbService.SearchInvType(partName);
            if (localTypes.NotNullOrEmpty())
            {
                localTypes.ForEach(p => searchInvTypes.Add(new TranslationSearchItem(p)));
            }
            return searchInvTypes;
        }
    }
}
