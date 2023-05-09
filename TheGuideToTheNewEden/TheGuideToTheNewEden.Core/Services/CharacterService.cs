using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Mail;
using TheGuideToTheNewEden.Core.Models.Wallet;
using TheGuideToTheNewEden.Core.Services.Api;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services
{
    public class CharacterService
    {
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

        public static async Task<List<Walletjournal>> GetWalletjournalAsync(int characterId, string token, int page)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterWalletJournal(characterId, token, page));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<List<Walletjournal>>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<List<WalletTransaction>> GetWalletTransactionsAsync(int characterId, string token, int page, bool needClientName = true)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterWalletTransactions(characterId, token, page));
            if (!string.IsNullOrEmpty(result))
            {
                var items = JsonConvert.DeserializeObject<List<WalletTransaction>>(result);
                if(items != null && items.Count != 0)
                {
                    var types = await InvTypeService.QueryTypesAsync(items.Select(p => p.Type_id).ToList());
                    if (types?.Count != 0)
                    {
                        var dic = types.ToDictionary(p => p.TypeID);
                        foreach (var item in items)
                        {
                            if (dic.TryGetValue(item.Type_id, out var invType))
                            {
                                item.Type_name = invType.TypeName;
                            }
                        }
                    }
                    if(needClientName)
                    {
                        var names = await UniverseService.SearchNameByIdsAsync(items.Select(p => p.Client_id).ToList());
                        if(names != null)
                        {
                            var dic = names.ToDictionary(p => p.Id);
                            foreach (var item in items)
                            {
                                if (dic.TryGetValue(item.Client_id, out var name))
                                {
                                    item.Client_name = name.Name;
                                }
                            }
                        }
                    }
                }
                return items;
            }
            else
            {
                return null;
            }
        }

        public static async Task<MailLabelRoot> GetMailLabelsAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterMailLabels(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<MailLabelRoot>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<List<MailList>> GetMailListsAsync(int characterId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterMailLists(characterId, token));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<List<MailList>>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<List<Mail>> GetMailsAsync(int characterId, string token, int label)
        {
            List<int> labelIds = new List<int>() { label };
            return await GetMailsAsync(characterId, token, labelIds);
        }
        public static async Task<List<Mail>> GetMailsAsync(int characterId, string token,List<int> labelIds = null,int lastMailId = 0)
        {
            string result;
            if (labelIds == null)
            {
                result = await HttpHelper.GetAsync(APIService.CharacterMail(characterId, token, lastMailId));
            }
            else
            {
                result = await HttpHelper.GetAsync(APIService.CharacterMailOfLables(characterId, token, labelIds, lastMailId));
            }
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<List<Mail>>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<Mail> GetMailDetailAsync(int characterId, string token, int mailId)
        {
            string result = await HttpHelper.GetAsync(APIService.CharacterMailDetail(characterId, token,mailId));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<Mail>(result);
            }
            else
            {
                return null;
            }
        }
    }
}
