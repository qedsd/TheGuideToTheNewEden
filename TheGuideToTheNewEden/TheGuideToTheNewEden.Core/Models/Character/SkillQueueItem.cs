using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillQueueItem: EVEStandard.Models.SkillQueue
    {
        /// <summary>
        /// UTC时间
        /// </summary>
        public DateTimeOffset FinishDateTime { get; set; }
        /// <summary>
        /// UTC时间
        /// </summary>
        public DateTimeOffset StartDateTime { get; set; }
        public string SkillName { get; set; }
        
        public string RemainTime
        {
            get
            {
                if(FinishDateTime == DateTimeOffset.MinValue)
                {
                    return "";
                }
                else
                {
                    TimeSpan timeSpan = IsRunning ? FinishDateTime - DateTime.UtcNow : FinishDateTime - StartDateTime;
                    if(timeSpan.Days >= 1)
                    {
                        return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}min";
                    }
                    else
                    {
                        return $"{timeSpan.Hours}h {timeSpan.Minutes}min";
                    }
                }
            }
        }
        public bool IsRunning
        {
            get => !(IsFinished || IsWaiting || IsPause);
        }
        public bool IsFinished { get => FinishDateTime != DateTimeOffset.MinValue && FinishDateTime < DateTime.UtcNow; }
        public bool IsWaiting 
        {
            get
            {
                return StartDateTime != DateTimeOffset.MinValue && FinishDateTime != DateTimeOffset.MinValue && StartDateTime > DateTimeOffset.UtcNow;
            }
        }
        public bool IsPause { get => FinishDate == null || StartDate == null; }
        public SkillQueueItem() { }
        public SkillQueueItem(EVEStandard.Models.SkillQueue skillQueueItem)
        {
            this.CopyFrom(skillQueueItem);
            FinishDateTime = FinishDate == null ? DateTimeOffset.MinValue : new DateTimeOffset(FinishDate.Value);
            StartDateTime = StartDate == null ? DateTimeOffset.MinValue : new DateTimeOffset(StartDate.Value);
        }
    }
}
