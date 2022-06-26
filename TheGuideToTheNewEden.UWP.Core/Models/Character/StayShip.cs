using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TheGuideToTheNewEden.UWP.Core.Models.Character
{
    public class StayShip
    {
        public long Ship_item_id { get; set; }
        private string ship_name;
        public string Ship_name
        {
            get
            {
                return ship_name;
            }
            set
            {
                ship_name = value;
            }
        }
        public int Ship_type_id { get; set; }
        public string Ship_type_name { get; set; }
    }
}
