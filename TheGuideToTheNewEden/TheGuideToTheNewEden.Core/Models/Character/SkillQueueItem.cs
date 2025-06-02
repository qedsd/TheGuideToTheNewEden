using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillQueueItem: ESI.NET.Models.Skills.SkillQueueItem
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
                if(string.IsNullOrEmpty(FinishDate) || string.IsNullOrEmpty(StartDate))
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
                return StartDateTime != DateTimeOffset.MinValue && FinishDateTime != DateTimeOffset.MinValue && StartDateTime > DateTime.UtcNow;
            }
        }
        public bool IsPause { get => string.IsNullOrEmpty(FinishDate) || string.IsNullOrEmpty(StartDate); }
        public SkillQueueItem() { }
        public SkillQueueItem(ESI.NET.Models.Skills.SkillQueueItem skillQueueItem)
        {
            this.CopyFrom(skillQueueItem);
            FinishDateTime = string.IsNullOrEmpty(FinishDate) ? DateTime.MinValue : DateTimeOffset.Parse(FinishDate);
            StartDateTime = string.IsNullOrEmpty(StartDate) ? DateTime.MinValue : DateTimeOffset.Parse(StartDate);
        }
    }
}
