using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.Models.Character;
using System.IO;
using System.Diagnostics;
using TheGuideToTheNewEden.WinUI.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using ESI.NET.Models.SSO;
using TheGuideToTheNewEden.Core.Services;
using System.Timers;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Xaml.Documents;
using ESI.NET.Models.Character;
using Microsoft.UI.Xaml;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    /// 管理角色授权及自带刷新当前角色token
    /// </summary>
    internal static class CharacterService
    {
        private static string ClientId = "8d0da2b105324ead932f60f32b1a55fb";//TODO:仅供测试
        private static string RedirectUri = "eveauth-qedsd-neweden2:///";
        private static readonly string AuthFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Auth.json");
        private static readonly List<string> EsiScopes = new List<string>()
        {
            "esi-location.read_location.v1",
            "esi-location.read_ship_type.v1",
            "esi-mail.organize_mail.v1",
            "esi-mail.read_mail.v1",
            "esi-mail.send_mail.v1",
            "esi-skills.read_skills.v1",
            "esi-skills.read_skillqueue.v1",
            "esi-wallet.read_character_wallet.v1",
            "esi-wallet.read_corporation_wallet.v1",
            "esi-search.search_structures.v1",
            "esi-clones.read_clones.v1",
            "esi-characters.read_contacts.v1",
            "esi-universe.read_structures.v1",
            "esi-killmails.read_killmails.v1",
            "esi-corporations.read_corporation_membership.v1",
            "esi-assets.read_assets.v1",
            "esi-planets.manage_planets.v1",
            "esi-ui.open_window.v1",
            "esi-ui.write_waypoint.v1",
            "esi-characters.write_contacts.v1",
            "esi-fittings.read_fittings.v1",
            "esi-fittings.write_fittings.v1",
            "esi-markets.structure_markets.v1",
            "esi-corporations.read_structures.v1",
            "esi-characters.read_loyalty.v1",
            "esi-characters.read_opportunities.v1",
            "esi-characters.read_chat_channels.v1",
            "esi-characters.read_medals.v1",
            "esi-characters.read_standings.v1",
            "esi-characters.read_agents_research.v1",
            "esi-industry.read_character_jobs.v1",
            "esi-markets.read_character_orders.v1",
            "esi-characters.read_blueprints.v1",
            "esi-characters.read_corporation_roles.v1",
            "esi-location.read_online.v1",
            "esi-contracts.read_character_contracts.v1",
            "esi-clones.read_implants.v1",
            "esi-killmails.read_corporation_killmails.v1",
            "esi-corporations.track_members.v1",
            "esi-wallet.read_corporation_wallets.v1",
            "esi-characters.read_notifications.v1",
            "esi-corporations.read_divisions.v1",
            "esi-corporations.read_contacts.v1",
            "esi-assets.read_corporation_assets.v1",
            "esi-corporations.read_titles.v1",
            "esi-corporations.read_blueprints.v1",
            "esi-contracts.read_corporation_contracts.v1",
            "esi-corporations.read_standings.v1",
            "esi-corporations.read_starbases.v1",
            "esi-industry.read_corporation_jobs.v1",
            "esi-markets.read_corporation_orders.v1",
            "esi-corporations.read_container_logs.v1",
            "esi-industry.read_character_mining.v1",
            "esi-industry.read_corporation_mining.v1",
            "esi-corporations.read_facilities.v1",
            "esi-corporations.read_medals.v1",
            "esi-characters.read_titles.v1",
            "esi-alliances.read_contacts.v1",
            "esi-characterstats.read.v1"
        };
        private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public static ObservableCollection<AuthorizedCharacterData> CharacterOauths { get; private set; }
        public static AuthorizedCharacterData CurrentCharacter { get; private set; }

        public static void Init()
        {
            CoreConfig.ClientId = GameServerSelectorService.Value == Core.Enums.GameServerType.Tranquility ? ClientId : SerenityAuthHelper.ClientId;
            CoreConfig.ESICallback = RedirectUri;
            CoreConfig.Scopes = EsiScopes;
            if (File.Exists(AuthFilePath))
            {
                string json = File.ReadAllText(AuthFilePath);
                CharacterOauths = JsonConvert.DeserializeObject<ObservableCollection<AuthorizedCharacterData>>(json);
            }
            else
            {
                CharacterOauths = new ObservableCollection<AuthorizedCharacterData>();
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

        public static void Add(AuthorizedCharacterData characterOauth)
        {
            CharacterOauths.Add(characterOauth);
            Save();
        }

        public static void Remove(AuthorizedCharacterData characterOauth)
        {
            if (CharacterOauths.Remove(characterOauth))
            {
                Save();
            }
        }

        public static async Task<AuthorizedCharacterData> AddAsync()
        {
            GetAuthorizeByBrower();
            var result = await AuthHelper.WaitingAuthAsync();
            if (result != null)
            {
                return await HandelProtocolAsync(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<string> GetAuthorizeResultAsync()
        {
            GetAuthorizeByBrower();
            return await AuthHelper.WaitingAuthAsync();
        }

        public static void GetAuthorizeByBrower()
        {
            string uri;
            if (Services.GameServerSelectorService.Value == Core.Enums.GameServerType.Tranquility)
            {
                uri = Core.Services.ESIService.SSO.CreateAuthenticationUrl(EsiScopes, Version);
            }
            else
            {
                uri = SerenityAuthHelper.GetAuthenticationUrl(EsiScopes);
            }
            var sInfo = new ProcessStartInfo(uri)
            {
                UseShellExecute = true,
            };
            Process.Start(sInfo);
        }

        public static async Task<AuthorizedCharacterData> HandelProtocolAsync(string uri)
        {
            try
            {
                var array = uri.Split(new char[2] { '=', '&' });
                string code = array[1];
                var token = await Core.Services.ESIService.SSO.GetToken(ESI.NET.Enumerations.GrantType.AuthorizationCode, code, Guid.NewGuid().ToString());
                if (token != null)
                {
                    var data = await Core.Services.ESIService.SSO.Verify(token);
                    if (data?.Token != null)
                    {
                        Add(data);
                        return data;
                    }
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
                return null;
            }
            return null;
        }

        public static void SetCurrentCharacter(AuthorizedCharacterData characterData)
        {
            ESIService.Current.EsiClient.SetCharacterData(characterData);
            CurrentCharacter = characterData;
            StartTimer();
        }

        public static AuthorizedCharacterData GetCurrentCharacter(AuthorizedCharacterData characterData)
        {
            return CurrentCharacter;
        }

        private static Timer RefreshTokenTimer;
        private static void StartTimer()
        {
            if(CurrentCharacter != null)
            {
                if (RefreshTokenTimer == null)
                {
                    RefreshTokenTimer = new Timer()
                    {
                        AutoReset = false
                    };
                    RefreshTokenTimer.Elapsed += RefreshTokenTimer_Elapsed;
                }
                var span = CurrentCharacter.ExpiresOn.ToLocalTime() - DateTime.Now;
                var interval = span.TotalMilliseconds - 60000;//提前一分钟刷新token
                _refreshing = interval < 0 ? true : false;
                interval = interval < 0 ? 1 : interval;
                RefreshTokenTimer.Interval = interval;
                RefreshTokenTimer.Start();
            }
        }
        private static bool _refreshing = false;
        private static async void RefreshTokenTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _refreshing = true;
            int tryCount = 0;
            while(tryCount < 3)
            {
                tryCount++;
                try
                {
                    if(await CurrentCharacter.RefreshTokenAsync())
                    {
                        break;
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                }
            }
            if(tryCount < 3)//成功刷新
            {
                var span = CurrentCharacter.ExpiresOn.ToLocalTime() - DateTime.Now;
                var interval = span.TotalMilliseconds - 60000;//提前一分钟刷新token
                RefreshTokenTimer.Interval = interval < 0 ? 100 : interval;
                RefreshTokenTimer.Start();
            }
            else//刷新失败
            {
                Core.Log.Error("刷新Token失败");
                //TODO:弹窗提示
            }
            _refreshing = false;
        }

        public static async Task WaitRefreshToken()
        {
            while (_refreshing)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// 获取默认角色
        /// 有当前角色则返回当前角色，无则返回首个
        /// 保证返回角色Token为可用状态
        /// </summary>
        /// <returns></returns>
        public static async Task<AuthorizedCharacterData> GetDefaultCharacterAsync()
        {
            AuthorizedCharacterData characterData = CurrentCharacter != null ? CurrentCharacter : CharacterOauths.FirstOrDefault();
            if(characterData != null)
            {
                if (!characterData.IsTokenValid())
                {
                    if (!await characterData.RefreshTokenAsync())
                    {
                        Core.Log.Error("获取默认角色失败，刷新Token失败");
                        return null;
                    }
                }
                return characterData;
            }
            else
            {
                Core.Log.Info("未添加角色");
                return null;
            }
        }

        public static AuthorizedCharacterData GetCharacter(int id)
        {
            return CharacterOauths.FirstOrDefault(p => p.CharacterID == id);
        }
    }
}
