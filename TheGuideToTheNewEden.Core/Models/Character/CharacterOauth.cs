using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class CharacterOauth
    {
        public int CharacterID { get; set; }
        public string CharacterName { get; set; }
        /// <summary>
        /// 不要直接使用，请使用GetAccessToken()
        /// </summary>
        public string Access_token { get; set; }
        public string Refresh_token { get; set; }
        /// <summary>
        /// 秒
        /// </summary>
        public int Expires_in { get; set; }
        public DateTime GetDateTime { get; set; }
        public bool IsActive { get; set; } = false;

        public CharacterOauth() { }
        public CharacterOauth(OauthToken oauthToken,VorifyToken vorifyToken)
        {
            CharacterID = vorifyToken.CharacterID;
            CharacterName = vorifyToken.CharacterName;
            Access_token = oauthToken.Access_token;
            Refresh_token = oauthToken.Refresh_token;
            Expires_in = oauthToken.Expires_in;
            GetDateTime = DateTime.Now;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            await Services.CharacterService.TryUpdateAccessTokenAsync(this);
            return Access_token;
        }

        public async Task<string> GetAccessToken(string clientId, string scope)
        {
            await Services.CharacterService.TryUpdateAccessTokenAsync(clientId, scope,this);
            return Access_token;
        }
    }
}
