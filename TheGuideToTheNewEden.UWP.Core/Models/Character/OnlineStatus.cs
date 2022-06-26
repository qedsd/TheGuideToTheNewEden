using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Character
{
    public class OnlineStatus
    {
        public DateTime Last_login { get; set; }
        public DateTime Last_logout { get; set; }
        public int Logins { get; set; }
        public bool Online { get; set; }
    }
}
