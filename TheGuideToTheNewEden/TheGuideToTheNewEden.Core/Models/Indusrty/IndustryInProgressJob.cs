using System;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>一个正在进行的 ESI 工业任务，用于占用角色槽位并显示在甘特图上。</summary>
    public sealed class IndustryInProgressJob
    {
        public long JobId { get; set; }
        public long CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public int ProductTypeId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int BlueprintTypeId { get; set; }
        public int ActivityId { get; set; }
        public int Runs { get; set; }
        public long QuantityProduced { get; set; }
        public double RemainingHours { get; set; }
        public DateTime EndDate { get; set; }
        public long FacilityId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsInvention => ActivityId == 8;
    }
}
