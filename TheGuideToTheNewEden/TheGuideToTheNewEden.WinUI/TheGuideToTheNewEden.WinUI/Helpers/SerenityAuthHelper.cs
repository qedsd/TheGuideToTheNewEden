using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    internal static class SerenityAuthHelper
    {
        private static readonly string host = "https://login.evepc.163.com/v2/oauth/authorize";
        private static readonly string client_id = "bc90aa496a404724a93f41b4f4e97761";
        public static string ClientId { get => client_id; }
        private static readonly string redirect_uri = "https://esi.evepc.163.com/ui/oauth2-redirect.html";
        private static readonly string state = "test";
        private static readonly string realm = "ESI";
        private static readonly string response_type = "code";
        private static readonly List<string> unvalidScopes = new List<string>()
        {
            "esi-wallet.read_corporation_wallet.v1",
            "esi-characters.read_chat_channels.v1"
        };
        public static string LogoffUrl { get => "https://login.evepc.163.com/account/logoff"; }
        /// <summary>
        /// 模拟一个device_id凑合用着
        /// </summary>
        /// <returns></returns>
        private static string GetDeviceId()
        {
            Random random = new Random();
            return random.Next(int.MaxValue).ToString();
        }
        private static string GetScope(List<string> esiScopes)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach(var scope in esiScopes)
            {
                if(unvalidScopes.Contains(scope))
                {
                    continue;
                }
                stringBuilder.Append(scope);
                stringBuilder.Append(' ');
            }
            stringBuilder.Remove(stringBuilder.Length - 1,1);
            return stringBuilder.ToString();
        }
        public static string GetAuthenticationUrl(List<string> esiScopes)
        {
            return $"{host}?response_type={response_type}&client_id={client_id}&redirect_uri={redirect_uri}&scope={GetScope(esiScopes)}&state={state}&realm={realm}&device_id={GetDeviceId()}";
        }
    }
}
