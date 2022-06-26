using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Core.Models.Character;
using Windows.Storage;

namespace TheGuideToTheNewEden.UWP.Services
{
    public static class CharacterService
    {
        private static string ClientId = "197e9346911a457abbdb717aac5eb454";
        private static string RedirectUri = "eveauth-qedsd-neweden:///";
        private static string Scope = "esi-universe.read_structures.v1 esi-markets.structure_markets.v1";
        private static string CharacterScope = "esi-skills.read_skills.v1 esi-skills.read_skillqueue.v1 esi-skills.read_skills.v1 esi-wallet.read_character_wallet.v1 " +
                "esi-wallet.read_character_wallet.v1 esi-wallet.read_character_wallet.v1 esi-characters.read_loyalty.v1 esi-location.read_location.v1 " +
                "esi-location.read_online.v1 esi-location.read_ship_type.v1 esi-mail.read_mail.v1 esi-contracts.read_character_contracts.v1 esi-universe.read_structures.v1 " +
            "esi-mail.organize_mail.v1 esi-clones.read_clones.v1 esi-clones.read_implants.v1 esi-markets.read_character_orders.v1 esi-markets.structure_markets.v1 " +
            "esi-ui.write_waypoint.v1";
        private static string FileName = "oauth.json";
        public static ObservableCollection<CharacterOauth> CharacterOauths { get; private set; }

        public static async Task InitAsync()
        {
            Core.Services.CharacterService.Init(ClientId, CharacterScope);
            var localFile = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
            //string path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
            if(localFile!=null)
            {
                string json = await FileIO.ReadTextAsync(localFile);
                CharacterOauths = JsonConvert.DeserializeObject<ObservableCollection<CharacterOauth>>(json);
            }
            else
            {
                CharacterOauths = new ObservableCollection<CharacterOauth>();
            }
        }
        private static async Task SaveAsync()
        {
            if(CharacterOauths != null)
            {
                string json = JsonConvert.SerializeObject(CharacterOauths);
                var localFile = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
                if (localFile == null)
                {
                    localFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName);
                }
                await FileIO.WriteTextAsync(localFile, json);
            }
        }

        public static async Task AddAsync(CharacterOauth characterOauth)
        {
            CharacterOauths.Add(characterOauth);
            await SaveAsync();
        }

        public static async Task RemoveAsync(CharacterOauth characterOauth)
        {
            if (CharacterOauths.Remove(characterOauth))
            {
                await SaveAsync();
            }
        }

        public static async void GetAuthorizeByBrower(Core.Enums.GameServerType server = Core.Enums.GameServerType.Tranquility)
        {
            string uri = null;
            if (server == Core.Enums.GameServerType.Tranquility)
            {
                uri = $"https://login.eveonline.com/v2/oauth/authorize?response_type=code&client_id={ClientId}&redirect_uri={RedirectUri}&state=test&scope={CharacterScope}";
            }
            else
            {

            }
            await Windows.System.Launcher.LaunchUriAsync(new Uri(uri));
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
                    await AddAsync(characterOauth);
                }
            }
        }
        public static void HandelStructureOauthProtocol(string uri)
        {

        }
    }
}
