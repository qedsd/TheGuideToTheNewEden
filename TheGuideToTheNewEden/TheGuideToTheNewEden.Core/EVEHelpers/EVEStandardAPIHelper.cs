using System;
using System.Collections.Generic;
using System.Text;
using EVEStandard.Models.API;
using EVEStandard.Models.SSO;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class EVEStandardAPIHelper
    {
        public static EVEStandard.EVEStandardAPI GetInstance(Core.Enums.GameServerType gameServer)
        {
            EVEStandard.Enumerations.DataSource dataSource = gameServer == Core.Enums.GameServerType.Tranquility ? EVEStandard.Enumerations.DataSource.Tranquility : EVEStandard.Enumerations.DataSource.Serenity;
            return new EVEStandard.EVEStandardAPI("TheGuideToTheNewEden", dataSource, TimeSpan.FromSeconds(30));
        }
        public static EVEStandard.Models.API.AuthDTO CreateEVEStandardSSO(ESI.NET.Models.SSO.AuthorizedCharacterData character)
        {
            if (character != null)
            {
                return new AuthDTO
                {
                    AccessToken = new AccessTokenDetails
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
