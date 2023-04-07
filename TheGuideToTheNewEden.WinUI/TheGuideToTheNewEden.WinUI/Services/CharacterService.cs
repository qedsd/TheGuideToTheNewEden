using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.Models.Character;
using System.IO;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class CharacterService
    {
        private static string ClientId = "";
        private static string RedirectUri = "eveauth-qedsd-neweden2://";
        private static string StructureMarketScope = "esi-universe.read_structures.v1 esi-markets.structure_markets.v1";
        private static string AllScope = "esi-skills.read_skills.v1 esi-skills.read_skillqueue.v1 esi-skills.read_skills.v1 esi-wallet.read_character_wallet.v1 " +
                "esi-wallet.read_character_wallet.v1 esi-wallet.read_character_wallet.v1 esi-characters.read_loyalty.v1 esi-location.read_location.v1 " +
                "esi-location.read_online.v1 esi-location.read_ship_type.v1 esi-mail.read_mail.v1 esi-contracts.read_character_contracts.v1 esi-universe.read_structures.v1 " +
            "esi-mail.organize_mail.v1 esi-clones.read_clones.v1 esi-clones.read_implants.v1 esi-markets.read_character_orders.v1 esi-markets.structure_markets.v1 " +
            "esi-ui.write_waypoint.v1";
        private static readonly string AuthFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Auth.json");
        public static ObservableCollection<CharacterOauth> CharacterOauths { get; private set; }
        public static void Init()
        {
            CoreConfig.ClientId = ClientId;
            CoreConfig.Scope = AllScope;
            if (File.Exists(AuthFilePath))
            {
                string json = File.ReadAllText(AuthFilePath);
                CharacterOauths = JsonConvert.DeserializeObject<ObservableCollection<CharacterOauth>>(json);
            }
            else
            {
                CharacterOauths = new ObservableCollection<CharacterOauth>();
            }
        }
        private static void Save()
        {
            if (CharacterOauths != null)
            {
                string json = JsonConvert.SerializeObject(CharacterOauths);
                string folder = Path.GetDirectoryName(AuthFilePath);
                if(!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                File.WriteAllText(AuthFilePath, json);
            }
        }

        public static void Add(CharacterOauth characterOauth)
        {
            CharacterOauths.Add(characterOauth);
            Save();
        }

        public static void Remove(CharacterOauth characterOauth)
        {
            if (CharacterOauths.Remove(characterOauth))
            {
                Save();
            }
        }

        public static void GetAuthorizeByBrower(Core.Enums.GameServerType server = Core.Enums.GameServerType.Tranquility)
        {
            string uri = null;
            if (server == Core.Enums.GameServerType.Tranquility)
            {
                uri = $"https://login.eveonline.com/v2/oauth/authorize?response_type=code&client_id={ClientId}&redirect_uri={RedirectUri}&state=test&scope={AllScope}";
            }
            else
            {

            }
            System.Diagnostics.Process.Start("explorer.exe", uri);
        }

        public static async void HandelCharacterOatuhProtocol(string uri)
        {
            var array = uri.Split(new char[2] { '=', '&' });
            string code = array[1];
            var token = await Core.Services.CharacterService.GetOauthTokenAsync(ClientId, code);
            if (token != null)
            {
                var vorifyToken = await Core.Services.CharacterService.GetVorifyTokenAsync(ClientId, token.Access_token);
                if (vorifyToken != null)
                {
                    CharacterOauth characterOauth = new CharacterOauth(token, vorifyToken);
                    Add(characterOauth);
                }
            }
        }
        public static void HandelStructureOauthProtocol(string uri)
        {

        }
    }
}
