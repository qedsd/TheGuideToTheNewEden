using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Services.Api;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class StructureService
    {
        public static async Task<StructureInfo> GetStructureInfoAsync(long structureId, string token)
        {
            string result = await HttpHelper.GetAsync(APIService.UniverseStructureInfo(structureId, token));
            if (!string.IsNullOrEmpty(result))
            {
                return JsonConvert.DeserializeObject<StructureInfo>(result);
            }
            else
            {
                return null;
            }
        }
    }
}
