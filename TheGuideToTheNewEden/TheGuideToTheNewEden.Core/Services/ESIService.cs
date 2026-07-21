using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVEStandard;
using TheGuideToTheNewEden.Core.Models.Character;
using System.Reflection;
using EVEStandard.Models.SSO;
using EVEStandard.Enumerations;

namespace TheGuideToTheNewEden.Core.Services
{
    public class ESIService
    {
        private static ESIService current;
        public static ESIService Current
        {
            get
            {
                if (current == null)
                {
                    current = new ESIService();
                }
                return current;
            }
        }
        /// <summary>
        /// 公开ESI
        /// </summary>
        public EVEStandardAPI EsiClient { get; private set; }
        public SSOv2 SSO { get; private set; }
        private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public ESIService()
        {
            EsiClient = GetDefaultESI();
            SSO = new SSOv2(GetDataSource(), Config.ESICallback, Config.ClientId, Config.ClientSecret);
            
        }
        public string GetAuthorizeUrl()
        {
            return SSO.AuthorizeToSSOBasicAuthUri(Version, Config.Scopes);
        }
        public async Task<AuthorizedCharacterData> VerifyAuthorization(string authorizationCode)
        {
            var acessToken = await SSO.VerifyAuthorizationForBasicAuthAsync(authorizationCode);
            if (acessToken != null)
            {
                var detail = await SSO.GetCharacterDetailsAsync(acessToken.AccessToken);
                if (detail != null)
                {
                    return new AuthorizedCharacterData()
                    {
                        Token = acessToken.AccessToken,
                        RefreshToken = acessToken.RefreshToken,
                        ExpiresOn = acessToken.ExpiresUtc,
                        CharacterID = detail.CharacterId,
                        CharacterName = detail.CharacterName,
                        CharacterOwnerHash = detail.CharacterOwnerHash,
                        Scopes = string.Join(",", detail.Scopes),
                    };
                }
            }
            return null;
        }
        public async Task<bool> Refresh(AuthorizedCharacterData authorizedCharacterData)
        {
            var accessToken = await GetNewBasicAuthAccessAndRefreshTokenAsync(authorizedCharacterData.RefreshToken);
            if (accessToken != null && !string.IsNullOrEmpty(accessToken.AccessToken))
            {
                authorizedCharacterData.Update(accessToken.RefreshToken, accessToken.AccessToken, accessToken.ExpiresUtc);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 重新实现SSO.GetNewBasicAuthAccessAndRefreshTokenAsync
        /// 加入失败抛出
        /// </summary>
        /// <param name="refreshToken"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<AccessTokenDetails> GetNewBasicAuthAccessAndRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(Config.ClientSecret))
            {
                throw new ArgumentNullException("You didn't provide a client secret when initalizing the SSOv2 class, one is required to utilize Basic Auth methods.");
            }

            var byteArray = Encoding.ASCII.GetBytes(Config.ClientId + ":" + Config.ClientSecret);

            var urlEncodedContent = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                };

            var stringContent = new FormUrlEncodedContent(urlEncodedContent);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(GetBaseURL() + "/oauth/token"),
                Method = HttpMethod.Post,
                Content = stringContent
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            HttpClient httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to refresh access token. Status code: {response.StatusCode}, Response: {json}");
            }
            return System.Text.Json.JsonSerializer.Deserialize<AccessTokenDetails>(json);
        }
        private string GetBaseURL()
        {
            return Config.DefaultGameServer switch
            {
                Enums.GameServerType.Tranquility => "https://login.eveonline.com/v2",
                Enums.GameServerType.Serenity => "https://login.evepc.163.com/v2",
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private static EVEStandard.Enumerations.DataSource GetDataSource()
        {
            return Config.DefaultGameServer == Enums.GameServerType.Tranquility ? EVEStandard.Enumerations.DataSource.Tranquility : EVEStandard.Enumerations.DataSource.Serenity;
        }

        public static EVEStandard.EVEStandardAPI GetDefaultESI()
        {
            return new EVEStandard.EVEStandardAPI("TheGuideToTheNewEden", GetDataSource(), EVEStandard.Enumerations.CompatibilityDate.v2025_12_16,TimeSpan.FromSeconds(30));
        }
        public static EVEStandard.Models.API.AuthDTO ToEVEStandardSSO(AuthorizedCharacterData character)
        {
            if (character != null)
            {
                return new EVEStandard.Models.API.AuthDTO
                {
                    AccessToken = new EVEStandard.Models.SSO.AccessTokenDetails
                    {
                        AccessToken = character.Token,
                        ExpiresUtc = character.ExpiresOn,
                        RefreshToken = character.RefreshToken
                    },
                    CharacterId = character.CharacterID,
                    Scopes = character.Scopes,
                };
            }
            else
            {
                return null;
            }
        }
    }
}
