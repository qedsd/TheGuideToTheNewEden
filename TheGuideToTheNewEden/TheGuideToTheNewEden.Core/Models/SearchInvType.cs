using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models
{
    public class SearchInvType
    {
        public int TypeID { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public bool IsLocal { get; set; }
        public SearchInvType()
        {

        }
        public SearchInvType(InvType invType)
        {
            TypeID = invType.TypeID;
            TypeName = invType.TypeName;
            Description = invType.Description;
        }
        public SearchInvType(InvTypeBase invType, bool isLocal = true)
        {
            TypeID = invType.TypeID;
            TypeName = invType.TypeName;
            Description = invType.Description;
            IsLocal = isLocal;
        }
    }
}
