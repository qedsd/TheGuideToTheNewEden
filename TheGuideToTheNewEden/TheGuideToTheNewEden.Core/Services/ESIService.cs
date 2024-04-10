using ESI.NET.Enumerations;
using ESI.NET;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Services.Api;
using Newtonsoft.Json;
using ESI.NET.Models.SSO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        public static SsoLogic SSO
        {
            get => Current.EsiClient.SSO;
        }
        /// <summary>
        /// 公开ESI
        /// </summary>
        public EsiClient EsiClient { get; private set; }
        public ESIService()
        {
            EsiClient = GetDefaultEsi();
        }

        public static EsiClient GetDefaultEsi()
        {
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = APIService.GetEsiUrl(),
                DataSource = Config.DefaultGameServer == Enums.GameServerType.Tranquility ? DataSource.Tranquility : DataSource.Serenity,
                ClientId = Config.ClientId,
                SecretKey = "Unneeded",
                CallbackUrl = Config.ESICallback,
                UserAgent = "TheGuideToTheNewEden",
            });
            return new ESI.NET.EsiClient(config);
        }
        public static async Task<SsoToken> GetToken(GrantType grantType, string code, string codeChallenge = null)
        {
            string body = "grant_type=" + grantType.ToEsiValue();
            switch (grantType)
            {
                case GrantType.AuthorizationCode:
                    body = body + "&code=" + code;
                    if (codeChallenge != null)
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(codeChallenge);
                        string base64 = Convert.ToBase64String(bytes).TrimEnd(new char[1] { '=' }).Replace('+', '-')
                            .Replace('/', '_');
                        body = body + "&code_verifier=" + base64 + "&client_id=" + Config.ClientId;
                    }

                    break;
                case GrantType.RefreshToken:
                    body = body + "&refresh_token=" + Uri.EscapeDataString(code);
                    if (codeChallenge != null)
                    {
                        body = body + "&client_id=" + Config.ClientId;
                    }

                    break;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"https://{APIService.GetSSOUrl()}/v2/oauth/token")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            if (codeChallenge == null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "Unneeded");
                request.Headers.Host = APIService.GetSSOUrl();
            }
            HttpClient client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = (DecompressionMethods.Deflate | DecompressionMethods.GZip)
            });
            client.DefaultRequestHeaders.Add("X-User-Agent", "TheGuideToTheNewEden");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            HttpResponseMessage response = await client.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            client.Dispose();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Core.Log.Error($"GetToken Failed: {response.StatusCode}");
                Core.Log.Error(content);
                throw new Exception($"GetToken Failed: {response.StatusCode}");
            }

            return JsonConvert.DeserializeObject<SsoToken>(content);
        }
        public static async Task<AuthorizedCharacterData> Verify(SsoToken token)
        {
            AuthorizedCharacterData authorizedCharacter = new AuthorizedCharacterData();
            try
            {
                HttpClient client = new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = (DecompressionMethods.Deflate | DecompressionMethods.GZip)
                });
                client.DefaultRequestHeaders.Add("X-User-Agent", "TheGuideToTheNewEden");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                string jwtksUrl = "https://" + APIService.GetSSOUrl() + "/oauth/jwks";
                JsonWebKeySet jwks = new JsonWebKeySet(await client.GetAsync(jwtksUrl).Result.Content.ReadAsStringAsync());
                client.Dispose();
                JsonWebKey jwk = jwks.Keys.First();
                ((SecurityTokenHandler)tokenHandler).ValidateToken(validationParameters: new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,//避免国服报错
                    ValidIssuer = "https://" + APIService.GetSSOUrl(),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwk,
                    ClockSkew = TimeSpan.FromSeconds(2.0)
                }, securityToken: token.AccessToken, validatedToken: out SecurityToken validatedToken);
                JwtSecurityToken jwtValidatedToken = validatedToken as JwtSecurityToken;
                string subjectClaim = jwtValidatedToken.Claims.SingleOrDefault((Claim c) => c.Type == "sub").Value;
                string nameClaim = jwtValidatedToken.Claims.SingleOrDefault((Claim c) => c.Type == "name").Value;
                string ownerClaim = jwtValidatedToken.Claims.SingleOrDefault((Claim c) => c.Type == "owner").Value;
                IEnumerable<Claim> returnedScopes = jwtValidatedToken.Claims.Where((Claim c) => c.Type == "scp");
                string scopesClaim = string.Join(" ", returnedScopes.Select((Claim s) => s.Value));
                authorizedCharacter.RefreshToken = token.RefreshToken;
                authorizedCharacter.Token = token.AccessToken;
                authorizedCharacter.CharacterName = nameClaim;
                authorizedCharacter.CharacterOwnerHash = ownerClaim;
                authorizedCharacter.CharacterID = int.Parse(subjectClaim.Split(':').Last());
                authorizedCharacter.ExpiresOn = jwtValidatedToken.ValidTo;
                authorizedCharacter.Scopes = scopesClaim;
                string url = APIService.GetEsiUrl() + "latest/characters/affiliation/?datasource=" + Config.DefaultGameServer.ToString().ToLower();
                StringContent body = new StringContent(JsonConvert.SerializeObject(new int[1] { authorizedCharacter.CharacterID }), Encoding.UTF8, "application/json");
                HttpClient client2 = new HttpClient();
                HttpResponseMessage characterResponse = await client2.PostAsync(url, body).ConfigureAwait(continueOnCapturedContext: false);
                client2.Dispose();
                if (characterResponse.StatusCode == HttpStatusCode.OK)
                {
                    EsiResponse<List<ESI.NET.Models.Character.Affiliation>> affiliations = new EsiResponse<List<ESI.NET.Models.Character.Affiliation>>(characterResponse, "Post|/character/affiliations/");
                    ESI.NET.Models.Character.Affiliation characterData = affiliations.Data.First();
                    authorizedCharacter.AllianceID = characterData.AllianceId;
                    authorizedCharacter.CorporationID = characterData.CorporationId;
                    authorizedCharacter.FactionID = characterData.FactionId;
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                throw ex;
            }

            return authorizedCharacter;
        }
    }
}
