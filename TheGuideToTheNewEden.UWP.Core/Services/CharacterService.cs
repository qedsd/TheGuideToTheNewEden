using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Core.Helpers;
using TheGuideToTheNewEden.UWP.Core.Models.Character;
using TheGuideToTheNewEden.UWP.Core.Services.Api;

namespace TheGuideToTheNewEden.UWP.Core.Services
{
    public class CharacterService
    {
        private static string ClientId;
        private static string Scope;
        public static void Init(string clientId, string scope)
        {
            ClientId = clientId;
            Scope = scope;
        }

        public static async Task<OauthToken> GetOauthTokenAsync(string clientId, string code)
        {
            var form = new Dictionary<string, string>();
            form.Add("grant_type", "authorization_code");
            form.Add("client_id", clientId);
            form.Add("code", code);
            string result = await Helpers.HttpHelper.PostAsync(Api.APIService.OauthToken(), form);
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<OauthToken>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<VorifyToken> GetVorifyTokenAsync(string clientId, string accessToken)
        {
            string result = await Helpers.HttpHelper.GetAsync(Api.APIService.VerifyToken(clientId, accessToken));
            return JsonConvert.DeserializeObject<VorifyToken>(result);
        }

        public static async Task<string> GetNewAccessTokenAsync(string clientId,string refreshToken, string scope)
        {
            var form = new Dictionary<string, string>();
            form.Add("grant_type", "refresh_token");
            form.Add("client_id", clientId);
            form.Add("refresh_token", refreshToken);
            form.Add("scope", scope);
            string result = await Helpers.HttpHelper.PostAsync(Api.APIService.OauthToken(), form);
            if (!string.IsNullOrEmpty(result))
            {
                var oauth =  JsonConvert.DeserializeObject<OauthToken>(result);
                return oauth?.Access_token;
            }
            else
            {
                return null;
            }
        }

        public static async Task TryUpdateAccessTokenAsync(string clientId, string scope, CharacterOauth characterOauth)
        {
            TimeSpan tsStart = new TimeSpan(characterOauth.GetDateTime.Ticks);
            TimeSpan tsEnd = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan span = tsEnd.Subtract(tsStart).Duration();
            if (span.TotalSeconds > characterOauth.Expires_in - 60)//即将60秒内超时则需要刷新token
            {
                characterOauth.Access_token = await GetNewAccessTokenAsync(clientId, characterOauth.Refresh_token, scope);
            }
        }

        public static async Task TryUpdateAccessTokenAsync(CharacterOauth characterOauth)
        {
            TimeSpan tsStart = new TimeSpan(characterOauth.GetDateTime.Ticks);
            TimeSpan tsEnd = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan span = tsEnd.Subtract(tsStart).Duration();
            if (span.TotalSeconds > characterOauth.Expires_in - 60)//即将60秒内超时则需要刷新token
            {
                characterOauth.Access_token = await GetNewAccessTokenAsync(ClientId, characterOauth.Refresh_token, Scope);
            }
        }

        public static async Task<Skill> GetSkillAsync(int characterId, string token)
        {
            var result = await HttpHelper.GetAsync(APIService.CharacterSkills(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                var skill = JsonConvert.DeserializeObject<Skill>(result);
                if(skill != null)
                {
                    return skill;
                }
            }
            return null;
        }
    }
}
