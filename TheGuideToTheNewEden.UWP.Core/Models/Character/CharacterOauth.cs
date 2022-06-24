using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.UWP.Core.Models.Character
{
    public class CharacterOauth
    {
        public int CharacterID { get; set; }
        public string CharacterName { get; set; }
        public string Access_token { get; set; }
        public string Refresh_token { get; set; }
        public int Expires_in { get; set; }
        public DateTime GetDateTime { get; set; }
        public bool IsActive { get; set; } = false;
    }
}
