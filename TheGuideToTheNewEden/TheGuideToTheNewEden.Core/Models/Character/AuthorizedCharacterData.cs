using EVEStandard.Models.API;
using EVEStandard.Models.SSO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    /// <summary>
    /// 本地 AuthorizedCharacterData，替代 ESI.NET 的 ESI.NET.Models.SSO.AuthorizedCharacterData
    /// 保持与原有 JSON 序列化格式兼容
    /// </summary>
    public class AuthorizedCharacterData
    {
        [JsonProperty("Token")]
        public string Token { get; set; }

        [JsonProperty("RefreshToken")]
        public string RefreshToken { get; set; }

        [JsonProperty("ExpiresOn")]
        public DateTime ExpiresOn { get; set; }

        [JsonProperty("CharacterID")]
        public long CharacterID { get; set; }

        [JsonProperty("CharacterName")]
        public string CharacterName { get; set; }

        [JsonProperty("CharacterOwnerHash")]
        public string CharacterOwnerHash { get; set; }

        [JsonProperty("Scopes")]
        public string Scopes { get; set; }

        [JsonProperty("AllianceID")]
        public long AllianceID { get; set; }

        [JsonProperty("CorporationID")]
        public long CorporationID { get; set; }

        [JsonProperty("FactionID")]
        public int FactionID { get; set; }

        /// <summary>
        /// 检查 token 是否未过期
        /// </summary>
        public bool IsTokenValid()
        {
            return ExpiresOn.ToLocalTime() > DateTime.Now;
        }

        private AuthDTO _auth;
        [JsonIgnore]
        public AuthDTO Auth
        {
            get
            {
                if(_auth == null)
                {
                    _auth= ToAuthDTO();
                }
                return _auth;
            }
        }

        /// <summary>
        /// 转换为 EVEStandard API 的 AuthDTO
        /// </summary>
        public AuthDTO ToAuthDTO()
        {
            if (string.IsNullOrEmpty(Token))
                return null;

            return new AuthDTO
            {
                AccessToken = new AccessTokenDetails
                {
                    AccessToken = Token,
                    ExpiresUtc = ExpiresOn,
                    RefreshToken = RefreshToken
                },
                CharacterId = CharacterID,
                Scopes = Scopes,
            };
        }

        /// <summary>
        /// 从另一个 AuthorizedCharacterData 复制数据
        /// </summary>
        public void CopyFrom(AuthorizedCharacterData source)
        {
            if (source == null)
                return;

            Token = source.Token;
            RefreshToken = source.RefreshToken;
            ExpiresOn = source.ExpiresOn;
            CharacterID = source.CharacterID;
            CharacterName = source.CharacterName;
            CharacterOwnerHash = source.CharacterOwnerHash;
            Scopes = source.Scopes;
            AllianceID = source.AllianceID;
            CorporationID = source.CorporationID;
            FactionID = source.FactionID;
        }

        public void Update(string refreshToken, string accessToken, DateTime expiresUtc)
        {
            RefreshToken = refreshToken;
            Token = accessToken;
            ExpiresOn = expiresUtc;
            if(_auth != null)
            {
                _auth.AccessToken.RefreshToken = refreshToken;
                _auth.AccessToken.AccessToken = accessToken;
                _auth.AccessToken.ExpiresUtc = expiresUtc;
            }
        }
    }
}
