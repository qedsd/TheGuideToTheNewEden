using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlayerStatus
{
    public class ServerStatusPerMonth
    {
        public int Month { get; set; }
        public int MaxPlayers { get; set; }
        public int AvgPlayers { get; set; }
        public int MinPlayers { get; set; }
    }
}
