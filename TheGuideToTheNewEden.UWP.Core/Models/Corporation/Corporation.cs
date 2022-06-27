using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Corporation
{
    public class Corporation
    {
        public int Alliance_id { get; set; }
        public int Ceo_id { get; set; }
        public int Creator_id { get; set; }
        public DateTime Date_founded { get; set; }
        public string Description { get; set; }
        public int Faction_id { get; set; }
        public int Home_station_id { get; set; }
        public int Member_count { get; set; }
        public string Name { get; set; }
        public long Shares { get; set; }
        public float Tax_rate { get; set; }
        public string Ticker { get; set; }
        public string Url { get; set; }
        public bool War_eligible { get; set; }
    }
}
