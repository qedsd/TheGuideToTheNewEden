using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Clone
{
    public class JumpClone
    {
        public ESI.NET.Models.Clones.JumpClone Clone { get; set; }
        public bool IsActive { get; set; }
        public string LocationName { get; set; }
        public List<InvType> CloneImplants { get; set; }
    }
}
