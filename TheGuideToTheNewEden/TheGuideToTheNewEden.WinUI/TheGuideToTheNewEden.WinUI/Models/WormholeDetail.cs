using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WinUI.Models
{
    public class WormholeDetail: Wormhole
    {
        public string ClassName { get; set; }
        public string PhenomenaName {  get; set; }
        public List<WormholePortal> StaticsPortals { get; set; }
        public List<WormholePortal> WanderingPortals { get; set; }
        public List<MapDenormalizeDetail> Planet {  get; set; }
        public List<MapDenormalizeDetail> Moon { get; set; }
        public List<MapDenormalizeDetail> Other { get; set; }

        public static WormholeDetail Create(Wormhole wormhole)
        {
            WormholeDetail wormholeDetail = new WormholeDetail();
            wormholeDetail.CopyFrom(wormhole);
            wormholeDetail.ClassName = Helpers.ResourcesHelper.GetString($"WormholePage_Class_{wormhole.Class}");
            wormholeDetail.PhenomenaName = Helpers.ResourcesHelper.GetString($"WormholePage_Phenomena_{wormhole.Phenomena}");
            if (!string.IsNullOrEmpty(wormholeDetail.Statics))
            {
                wormholeDetail.StaticsPortals = new List<WormholePortal>();
                foreach (var portalId in wormholeDetail.Statics.Split(','))
                {
                    var portal = Core.Services.DB.WormholeService.QueryPortal(int.Parse(portalId));
                    portal.DestinationName = Helpers.ResourcesHelper.GetString($"WormholePage_Class_{portal.Destination}");
                    wormholeDetail.StaticsPortals.Add(portal);
                }
            }
            if (!string.IsNullOrEmpty(wormholeDetail.Wanderings))
            {
                wormholeDetail.WanderingPortals = new List<WormholePortal>();
                foreach (var portalId in wormholeDetail.Wanderings.Split(','))
                {
                    var portal = Core.Services.DB.WormholeService.QueryPortal(int.Parse(portalId));
                    portal.DestinationName = Helpers.ResourcesHelper.GetString($"WormholePage_Class_{portal.Destination}");
                    wormholeDetail.WanderingPortals.Add(portal);
                }
            }
            var allMapDenormalizes = MapDenormalizeService.QueryBySolarSystemID(wormhole.Id, true);

            wormholeDetail.Planet = new List<MapDenormalizeDetail>();
            wormholeDetail.Moon = new List<MapDenormalizeDetail>();
            wormholeDetail.Other = new List<MapDenormalizeDetail>();

            foreach (var mapDenormalize in allMapDenormalizes)
            {
                switch (mapDenormalize.GroupID)
                {
                    case 6:
                    case 7: wormholeDetail.Planet.Add(mapDenormalize); break;
                    case 8: wormholeDetail.Moon.Add(mapDenormalize); break;
                    default: wormholeDetail.Other.Add(mapDenormalize); break;
                }
            }
            return wormholeDetail;
        }
    }
}
