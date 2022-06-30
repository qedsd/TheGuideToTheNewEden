using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.Services.Api;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class UniverseService
    {
        private static string ClientId { get => Config.ClientId; }
        private static string Scope { get => Config.Scope; }

        public static async Task<List<SearchName>> SearchNameByIdsAsync(List<int> ids, bool isUnique = false)
        {
            List<int> uniqueIds;
            if(isUnique)
            {
                uniqueIds = ids;
            }
            else
            {
                HashSet<int> sets = new HashSet<int>();
                ids.ForEach(p => sets.Add(p));
                uniqueIds = sets.ToList();
            }
            
            string result = await HttpHelper.PostJsonAsync(APIService.UniverseName(),JsonConvert.SerializeObject(uniqueIds));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<List<SearchName>>(result);
            }
            else
            {
                return null;
            }
        }
    }
}
