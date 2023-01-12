using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Clone
{
    public class Clone
    {
        public HomeLocation Home_Location { get; set; }
        public List<JumpClone> Jump_Clones { get; set; }
        public DateTime Last_Clone_Jump_Date { get; set; }
        public DateTime Last_Station_Change_Date { get; set; }
    }
}
