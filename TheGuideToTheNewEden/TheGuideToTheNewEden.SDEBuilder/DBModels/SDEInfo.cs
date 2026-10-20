using System;
using SqlSugar;

namespace TheGuideToTheNewEden.SDEBuilder.DBModels
{
    [SugarTable("sdeInfo")]
    public class SDEInfo
    {
        [SugarColumn(IsPrimaryKey = true)]
        public string Id { get; set; }
        public int BuildNumber { get; set; }
        public DateTime ReleaseDate { get; set; }
        public SDEInfo() { }
        public SDEInfo(Models.SDEInfo model)
        {
            Id = model.Id;
            BuildNumber = model.BuildNumber;
            ReleaseDate = model.ReleaseDate;
        }
    }
}
