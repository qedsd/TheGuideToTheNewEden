using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVEStandard;
using TheGuideToTheNewEden.Core.Models.Character;
using ESI.NET;
using System.Reflection;

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
            SSO = new SSOv2(GetDataSource(), Config.ESICallback, Config.ClientId);
            
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
            var accessToken = await SSO.GetNewBasicAuthAccessAndRefreshTokenAsync(authorizedCharacterData.RefreshToken, authorizedCharacterData.Scopes.Split(',').ToList());
            if (accessToken != null)
            {
                authorizedCharacterData.Update(accessToken.RefreshToken, accessToken.AccessToken, accessToken.ExpiresUtc);
                return true;
            }
            else
            {
                return false;
            }
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
