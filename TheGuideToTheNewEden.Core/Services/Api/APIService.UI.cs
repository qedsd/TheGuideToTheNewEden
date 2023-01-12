using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Services.Api
{
    public static partial class APIService
    {
        /// <summary>
        /// 检验token
        /// </summary>
        /// <returns></returns>
        public static string UIAutopilot(GameServerType server, bool isAddToBeginning, bool isClearOtherWaypoints, long destinationId, string token)
        {
            if (server == GameServerType.Tranquility)
                return $"{TranquilityUri}/ui/autopilot/waypoint/?add_to_beginning={isAddToBeginning}&clear_other_waypoints={isClearOtherWaypoints}/datasource=tranquility&destination_id={destinationId}&token={token}";
            else
                return $"{SerenityUri}/ui/autopilot/waypoint/?add_to_beginning={isAddToBeginning}&clear_other_waypoints={isClearOtherWaypoints}/datasource=serenity&destination_id={destinationId}&token={token}";
        }
    }
}
