using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public class InvNameService
    {
        public static InvName Query(int id)
        {
            return DBService.MainDb.Queryable<InvName>().First(p => p.ID == id);
        }
    }
}
