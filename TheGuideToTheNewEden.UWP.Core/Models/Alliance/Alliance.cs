using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Alliance
{
    public class Alliance
    {
        public int Creator_corporation_id { get; set; }
        public int Creator_id { get; set; }
        public DateTime Date_founded { get; set; }
        public int Executor_corporation_id { get; set; }
        public int Faction_id { get; set; }
        public string Name { get; set; }
        public string Ticker { get; set; }
    }
}
