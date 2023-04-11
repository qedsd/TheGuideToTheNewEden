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

        public ESIService()
        {
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = Config.DefaultGameServer == Enums.GameServerType.Tranquility? "https://esi.evetech.net/": "https://esi.evepc.163.com/",
                DataSource = DataSource.Tranquility,
                ClientId = Config.ClientId,
                SecretKey = "Unneeded",
                CallbackUrl = Config.ESICallback,
                UserAgent = "TheGuideToTheNewEden",
            });
        }
    }
}
