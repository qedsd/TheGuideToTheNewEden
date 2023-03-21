﻿using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;

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

        public static async Task<List<InvType>> QueryTypesAsync(List<int> ids)
        {
            var types = await DBService.MainDb.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToListAsync();
            if (DBService.NeedLocalization)
            {
                await LocalDbService.TranInvTypesAsync(types);
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
    }
}