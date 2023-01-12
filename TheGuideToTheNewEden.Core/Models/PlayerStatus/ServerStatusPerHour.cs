using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.PlayerStatus
{
    public class ServerStatusPerHour
    {
        public DateTime Time { get; set; }
        public int Players { get; set; }
    }
}
