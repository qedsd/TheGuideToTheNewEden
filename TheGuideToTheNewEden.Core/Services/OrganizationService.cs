using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.Alliance;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Corporation;
using TheGuideToTheNewEden.Core.Services.Api;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class OrganizationService
    {
        public static async Task<Affiliation> GetAffiliationAsync(int characterId, bool isDetail = true)
        {
            List<int> characterIds = new List<int>(1) { characterId };
            var items = await GetAffiliationAsync(characterIds, isDetail);
            return items?.FirstOrDefault();
        }
        public static async Task<List<Affiliation>> GetAffiliationAsync(List<int> characterIds,bool isDetail = true)
        {
            //StringBuilder stringBuilder = new StringBuilder();
            //characterIds.ForEach(p =>
            //{
            //    stringBuilder.Append(p);
            //    stringBuilder.Append(',');
            //});
            //stringBuilder.Remove(stringBuilder.Length - 1, 1);
            var result = await HttpHelper.PostJsonAsync(APIService.CharacterAffiliation(),JsonConvert.SerializeObject(characterIds));
            if (!string.IsNullOrEmpty(result))
            {
                var aff =  JsonConvert.DeserializeObject<List<Affiliation>>(result);
                if(aff?.Count != 0)
                {
                    if(isDetail)
                    {
                        await ThreadHelper.RunAsync(aff, GetAffiliationDetail);
                    }
                }
                return aff;
            }
            else
            {
                return null;
            }
        }
        private static void GetAffiliationDetail(Affiliation affiliation)
        {
            affiliation.CharacterInfo = CharacterService.GetCharacterInfoAsync(affiliation.Character_id).GetAwaiter().GetResult();
            affiliation.Corporation = GetCorporationAsync(affiliation.Corporation_id).GetAwaiter().GetResult();
            affiliation.Alliance = GetAllianceAsync(affiliation.Alliance_id).GetAwaiter().GetResult();
        }
        public static async Task<Corporation> GetCorporationAsync(int id)
        {
            if (id <= 0)
            {
                return null;
            }
            var result = await HttpHelper.GetAsync(APIService.CorporationInfo(id));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<Corporation>(result);
            }
            else
            {
                return null;
            }
        }

        public static async Task<Alliance> GetAllianceAsync(int id)
        {
            if(id <= 0)
            {
                return null;
            }
            var result = await HttpHelper.GetAsync(APIService.AllianceInfo(id));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<Alliance>(result);
            }
            else
            {
                return null;
            }
        }
    }
}
