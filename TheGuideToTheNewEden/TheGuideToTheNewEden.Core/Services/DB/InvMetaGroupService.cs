using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class InvMetaGroupService
    {
        public static List<InvMetaGroup> QueryAll()
            => DBService.MainDb.Queryable<InvMetaGroup>()
                .OrderBy(p => p.MetaGroupID)
                .ToList();
    }
}
