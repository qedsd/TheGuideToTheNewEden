using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class VerifyToken
    {
        public int CharacterID { get; set; }
        public string CharacterName { get; set; }
        public DateTime ExpiresOn { get; set; }
        public string Scopes { get; set; }
        public string TokenType { get; set; }
        public string CharacterOwnerHash { get; set; }
        public string ClientID { get; set; }
        
    }
}
