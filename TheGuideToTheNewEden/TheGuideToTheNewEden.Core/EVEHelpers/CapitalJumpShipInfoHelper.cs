using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.EVEHelpers
{
    public static class CapitalJumpShipInfoHelper
    {
        private static object _locker = new object();
        private static List<CapitalJumpShipInfo> _infos;
        public static List<CapitalJumpShipInfo> GetInfos()
        {
            lock(_locker)
            {
                if (_infos == null)
                {
                    var json = System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "CapitalJumpShipInfo.json"));
                    if (json != null)
                    {
                        _infos = JsonConvert.DeserializeObject<List<CapitalJumpShipInfo>>(json);
                        foreach (var info in _infos)
                        {
                            info.InvMarketGroup = Core.Services.DB.InvMarketGroupService.Query(info.GroupID);
                            info.InvType = Core.Services.DB.InvTypeService.QueryType(info.TypeID);
                        }
                    }
                }
                return _infos;
            }
        }
    }
}
