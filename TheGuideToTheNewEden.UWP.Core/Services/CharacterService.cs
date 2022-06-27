using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services.Api;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services
{
    public class CharacterService
    {
        private static string ClientId { get => Config.ClientId; }
        private static string Scope{get=> Config.Scope; }

        public static async Task<OauthToken> GetOauthTokenAsync(string clientId, string code)
        {
            var form = new Dictionary<string, string>();
            form.Add("grant_type", "authorization_code");
            form.Add("client_id", clientId);
            form.Add("code", code);
            string result = await Helpers.HttpHelper.PostAsync(Api.APIService.OauthToken(), form);
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<OauthToken>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<VorifyToken> GetVorifyTokenAsync(string clientId, string accessToken)
        {
            string result = await Helpers.HttpHelper.GetAsync(Api.APIService.VerifyToken(clientId, accessToken));
            return JsonConvert.DeserializeObject<VorifyToken>(result);
        }

        public static async Task<string> GetNewAccessTokenAsync(string clientId,string refreshToken, string scope)
        {
            var form = new Dictionary<string, string>();
            form.Add("grant_type", "refresh_token");
            form.Add("client_id", clientId);
            form.Add("refresh_token", refreshToken);
            form.Add("scope", scope);
            string result = await Helpers.HttpHelper.PostAsync(Api.APIService.OauthToken(), form);
            if (!string.IsNullOrEmpty(result))
            {
                var oauth =  JsonConvert.DeserializeObject<OauthToken>(result);
                return oauth?.Access_token;
            }
            else
            {
                return null;
            }
        }

        public static async Task TryUpdateAccessTokenAsync(string clientId, string scope, CharacterOauth characterOauth)
        {
            TimeSpan tsStart = new TimeSpan(characterOauth.GetDateTime.Ticks);
            TimeSpan tsEnd = new TimeSpan(DateTime.Now.Ticks);
            TimeSpan span = tsEnd.Subtract(tsStart).Duration();
            if (span.TotalSeconds > characterOauth.Expires_in - 60)//即将60秒内超时则需要刷新token
            {
                characterOauth.Access_token = await GetNewAccessTokenAsync(clientId, characterOauth.Refresh_token, scope);
            }
        }

        public static async Task TryUpdateAccessTokenAsync(CharacterOauth characterOauth)
        {
            await TryUpdateAccessTokenAsync(ClientId, Scope, characterOauth);
        }

        /// <summary>
        /// 获取角色的技能情况
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<Skill> GetSkillAsync(int characterId, string token)
        {
            var result = await HttpHelper.GetAsync(APIService.CharacterSkills(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                var skill = JsonConvert.DeserializeObject<Skill>(result);
                if(skill != null)
                {
                    var types = await InvTypeService.QueryTypesAsync(skill.Skills.Select(p => p.Skill_id).ToList());
                    Dictionary<int, SkillItem> dic = new Dictionary<int, SkillItem>();
                    skill.Skills.ForEach(p => dic.Add(p.Skill_id, p));
                    foreach(var type in types)
                    {
                        if(dic.TryGetValue(type.TypeID, out var skillItem))
                        {
                            skillItem.Skill_name = type.TypeName;
                            skillItem.Skill_des = type.Description;
                        }
                    }
                    return skill;
                }
            }
            return null;
        }
        /// <summary>
        /// 获取角色的技能情况
        /// 返回结果包含各技能分组
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<Skill> GetSkillWithGroupAsync(int characterId, string token)
        {
            var skill = await GetSkillAsync(characterId, token);
            if (skill != null)
            {
                var skillGroup = await InvGroupService.QuerySkillGroupsAsync();
                if(skillGroup != null)
                {
                    foreach(var p in skill.Skills)
                    {
                        var group = skillGroup.FirstOrDefault(p2=>p2.SkillIds.Contains(p.Skill_id));
                        if(group != null)
                        {
                            if(group.Skills == null)
                            {
                                group.Skills = new List<SkillItem>();
                            }
                            group.Skills.Add(p);
                        }
                    }
                }
                skill.SkillGroups = skillGroup;
            }
            return skill;
        }

        public static async Task<double> GetWalletBalanceAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterWallet(characterId, token));
            if(double.TryParse(result,out var balance))
            {
                return balance;
            }
            else
            {
                return -1;
            }
        }

        public static async Task<List<Loyalty>> GetLoyaltysAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterLoyaltyPoint(characterId, token));
            if(!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<List<Loyalty>>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<OnlineStatus> GetOnlineStatusAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterOnline(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<OnlineStatus>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<Location> GetLocationAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterLocation(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                var location = JsonConvert.DeserializeObject<Location>(result);
                if(location != null)
                {
                    var system = await MapSolarSystemService.QueryAsync(location.Solar_system_id);
                    location.Solar_system = system?.SolarSystemName;
                    if(location.Station_id != 0)
                    {
                        var station = await StaStationService.QueryAsync(location.Station_id);
                        location.Station = station?.StationName;
                    }
                    else
                    {
                        var structure = await StructureService.GetStructureInfoAsync(location.Structure_id, token);
                        location.Structure = structure?.Name;
                    }
                    return location;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static async Task<StayShip> GetStayShipAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterShip(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                var ship = JsonConvert.DeserializeObject<StayShip>(result);
                if (ship != null)
                {
                    var type = await InvTypeService.QueryTypeAsync(ship.Ship_type_id);
                    if(type!=null)
                    {
                        ship.Ship_type_name = type.TypeName;
                    }
                    return ship;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static async Task<CharacterInfo> GetCharacterInfoAsync(int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }
            var result = await HttpHelper.GetAsync(APIService.CharacterInfo(characterId));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<CharacterInfo>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<List<SkillQueue>> GetSkillQueuesAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterSkillqueue(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                var queues = JsonConvert.DeserializeObject<List<SkillQueue>>(result);
                if (queues?.Count!=0)
                {
                    var types = await InvTypeService.QueryTypesAsync(queues.Select(p => p.Skill_id).ToList());
                    if (types?.Count != 0)
                    {
                        var dic = types.ToDictionary(p => p.TypeID);
                        foreach(var q in queues)
                        {
                            if(dic.TryGetValue(q.Skill_id,out var invType))
                            {
                                q.Skill_name = invType.TypeName;
                                q.Skill_des = invType.Description;
                            }
                        }
                    }
                    return queues;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
}
