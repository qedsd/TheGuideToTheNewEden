using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Core.DBModels;

namespace TheGuideToTheNewEden.UWP.Core.Services.DB
{
    public static class ZHDBService
    {
        public static async Task<List<InvType>> TranInvTypesAsync(List<int> invTypeIds)
        {
            return await Task.Run(() =>
            {
                SqlSugarClient db = new SqlSugarClient(Config.ZHDBConnectionConfig);
                return db.Queryable<InvType>().Where(p=> invTypeIds.Contains(p.TypeID)).ToList();
            });
        }
        public static async Task<InvType> TranInvTypeAsync(int invTypeId)
        {
            SqlSugarClient db = new SqlSugarClient(Config.ZHDBConnectionConfig);
            return await db.Queryable<InvType>().FirstAsync(p => invTypeId == p.TypeID);
        }

        public static async Task TranInvTypes(List<InvType> invTypes)
        {
            Dictionary<int, InvType> keyValuePairs = new Dictionary<int, InvType>();
            foreach (InvType invType in invTypes)
            {
                keyValuePairs.Add(invType.TypeID, invType);
            }
            var results = await TranInvTypesAsync(invTypes.Select(p => p.TypeID).ToList());
            foreach(var result in results)
            {
                keyValuePairs.TryGetValue(result.TypeID, out var keyValue);
                {
                    keyValue.TypeName = result.TypeName;
                    keyValue.Description = result.Description;
                }
            }
            keyValuePairs.Clear();
            keyValuePairs = null;
        }

        public static async Task TranInvType(InvType invType)
        {
            var type = await TranInvTypeAsync(invType.TypeID);
            invType.TypeName = type.TypeName;
            invType.Description = type.Description;
        }
    }
}
