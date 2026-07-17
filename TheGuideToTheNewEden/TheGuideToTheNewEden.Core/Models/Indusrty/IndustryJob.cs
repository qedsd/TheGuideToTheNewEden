using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    public class IndustryJob : EVEStandard.Models.IndustryJob
    {
        public IndustryJob(EVEStandard.Models.IndustryJob job)
        {
            this.CopyFrom(job);
            Span = TimeSpan.FromSeconds(Duration);
        }
        public InvType Blueprint { get; set; }
        public InvType Product { get; set; }
        public IdNameLong Location { get; set; }
        public TimeSpan Span{ get; set; }
        public string StatusDesc{ get; set; }
    }
}
