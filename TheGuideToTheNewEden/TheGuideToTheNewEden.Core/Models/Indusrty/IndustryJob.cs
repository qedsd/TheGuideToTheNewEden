using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    public class IndustryJob : ESI.NET.Models.Industry.Job
    {
        public IndustryJob(ESI.NET.Models.Industry.Job job)
        {
            this.CopyFrom(job);
        }
        public InvType Blueprint { get; set; }
        public InvType Product { get; set; }
        public IdName Location { get; set; }

        public static IndustryJob Create(ESI.NET.Models.Industry.Job job)
        {
            IndustryJob industryJob = new IndustryJob(job);
            if(job.StationId < 70000000)//空间站
            {
                var sta = Services.DB.StaStationService.Query((int)job.StationId);
                if(sta != null)
                {
                    industryJob.Location = new IdName(sta.StationID, sta.StationName, IdName.CategoryEnum.Station);
                }
                else
                {
                    industryJob.Location = new IdName((int)job.StationId, job.StationId.ToString(), IdName.CategoryEnum.Station);
                }
            }
            else
            {
                var sta = IDNameService.GetById((int)job.StationId);
                if(sta != null)
                {
                    industryJob.Location = sta;
                }
                else
                {
                    industryJob.Location = new IdName((int)job.StationId, job.StationId.ToString(), IdName.CategoryEnum.Structure);
                }
            }
            industryJob.Blueprint = Services.DB.InvTypeService.QueryType(job.BlueprintTypeId);
            industryJob.Product = Services.DB.InvTypeService.QueryType(job.ProductTypeId);
            return industryJob;
        }
    }
}
