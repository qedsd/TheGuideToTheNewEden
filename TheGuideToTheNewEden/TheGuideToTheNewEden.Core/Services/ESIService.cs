using ESI.NET.Enumerations;
using ESI.NET;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

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
                EsiUrl = Config.DefaultGameServer == Enums.GameServerType.Tranquility ? "https://esi.evetech.net/" : "https://esi.evepc.163.com/",
                DataSource = Config.DefaultGameServer == Enums.GameServerType.Tranquility ? DataSource.Tranquility : DataSource.Singularity,
                ClientId = Config.ClientId,
                SecretKey = "Unneeded",
                CallbackUrl = Config.ESICallback,
                UserAgent = "TheGuideToTheNewEden",
            });
            return new ESI.NET.EsiClient(config);
        }
    }
}
