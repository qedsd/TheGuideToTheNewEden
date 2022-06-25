using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Core.Models.Character;

namespace TheGuideToTheNewEden.UWP.Services
{
    public static class CharacterService
    {
        public static string ClientId = "197e9346911a457abbdb717aac5eb454";
        public static string RedirectUri = "eveauth-qedsd-neweden:///";
        public static string Scope = "esi-universe.read_structures.v1 esi-markets.structure_markets.v1";
        public static string CharacterScope = "esi-skills.read_skills.v1 esi-skills.read_skillqueue.v1 esi-skills.read_skills.v1 esi-wallet.read_character_wallet.v1 " +
                "esi-wallet.read_character_wallet.v1 esi-wallet.read_character_wallet.v1 esi-characters.read_loyalty.v1 esi-location.read_location.v1 " +
                "esi-location.read_online.v1 esi-location.read_ship_type.v1 esi-mail.read_mail.v1 esi-contracts.read_character_contracts.v1 esi-universe.read_structures.v1 " +
            "esi-mail.organize_mail.v1 esi-clones.read_clones.v1 esi-clones.read_implants.v1 esi-markets.read_character_orders.v1 esi-markets.structure_markets.v1 " +
            "esi-ui.write_waypoint.v1";
        public static async void GetAuthorizeByBrower(Core.Enums.GameServerType server = Core.Enums.GameServerType.Tranquility)
        {
            string uri = null;
            if (server == Core.Enums.GameServerType.Tranquility)
            {
                uri = $"https://login.eveonline.com/v2/oauth/authorize?response_type=code&client_id={ClientId}&redirect_uri={RedirectUri}&state=test&scope={CharacterScope}";
            }
            else
            {

            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri(uri));
        }

        public static async void HandelProtocol(string uri)
        {
            var array = uri.Split(new char[2] { '=', '&' });
            string code = array[1];
            var form = new Dictionary<string, string>();
            form.Add("grant_type", "authorization_code");
            form.Add("client_id", ClientId);
            form.Add("code", code);
            string result = await Core.Helpers.HttpHelper.PostAsync(Core.Services.Api.APIService.OauthToken(), form);
            if(!string.IsNullOrEmpty(result))
            {
                var token = JsonConvert.DeserializeObject<OauthToken>(result);
                if(token != null)
                {
                    var vorifyToken = await GetVorifyTokenAsync(token.Access_token);
                    if(vorifyToken != null)
                    {
                        CharacterOauth characterOauth = new CharacterOauth(token, vorifyToken);
                        //save
                    }
                }
            }
        }

        public static async Task<VorifyToken> GetVorifyTokenAsync(string access_token)
        {
            string result = await Core.Helpers.HttpHelper.GetAsync(Core.Services.Api.APIService.VerifyToken(ClientId, access_token));
            return JsonConvert.DeserializeObject<VorifyToken>(result);
        }
    }
}
