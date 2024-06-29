using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Map;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class RegionMapHelper
    {
        private static Dictionary<int, RegionPosition> positionDic;
        public static Dictionary<int, RegionPosition> PositionDic
        {
            get
            {
                if (positionDic == null)
                {
                    var json = System.IO.File.ReadAllText(Config.RegionMapPath);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var list = JsonConvert.DeserializeObject<List<RegionPosition>>(json);
                        if (list.NotNullOrEmpty())
                        {
                            positionDic = list.ToDictionary(p => p.RegionId);
                        }
                    }
                }
                return positionDic;
            }
        }
    }
}
