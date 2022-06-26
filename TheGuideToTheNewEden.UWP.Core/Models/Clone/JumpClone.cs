using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Clone
{
    public class JumpClone
    {
        public List<int> Implants { get; set; }
        public List<CloneImplant> CloneImplant { get; set; }
        public int Jump_Clone_Id { get; set; }
        public long Location_Id { get; set; }
        public string Location_Type { get; set; }
        public string Location_Name { get; set; }
        public string Name { get; set; }
    }
}
