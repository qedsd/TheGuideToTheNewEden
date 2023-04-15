using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillQueueItem: ESI.NET.Models.Skills.SkillQueueItem
    {
        public string SkillName { get; set; }
        public string RemainTime
        {
            get
            {
                if(string.IsNullOrEmpty(FinishDate) || string.IsNullOrEmpty(StartDate))
                {
                    return "已暂停";
                }
                else
                {
                    return (DateTime.Parse(FinishDate) - DateTime.Parse(StartDate)).ToString();
                }
            }
        }
        public bool IsRunning
        {
            get => !(string.IsNullOrEmpty(FinishDate) || string.IsNullOrEmpty(StartDate));
        }
        public SkillQueueItem() { }
        public SkillQueueItem(ESI.NET.Models.Skills.SkillQueueItem skillQueueItem)
        {
            this.CopyFrom(skillQueueItem);
        }
    }
}
