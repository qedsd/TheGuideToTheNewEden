using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.DBModels
{
    public class MapDenormalizeDetail: MapDenormalize
    {
        public MapDenormalizeDetail() { }
        public MapDenormalizeDetail(MapDenormalize mapDenormalize)
        {
            this.CopyFrom(mapDenormalize);
        }
        public InvType Type {  get; set; }
    }
}
