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
        private static List<string> EsiScopes { get => Services.Settings.ESIScopeService.Current.GetSelectedScopes(); }
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

        public static async Task<string> GetAuthorizeResultAsync()
        {
            GetAuthorizeByBrower();
            return await AuthHelper.WaitingAuthAsync(System.Threading.CancellationToken.None);
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
                var token = await Core.Services.ESIService.GetToken(ESI.NET.Enumerations.GrantType.AuthorizationCode, code, Guid.NewGuid().ToString());
                if (token != null)
                {
                    var data = await Core.Services.ESIService.Verify(token);
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
        }

        public static AuthorizedCharacterData GetCurrentCharacter()
        {
            return CurrentCharacter;
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
