using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillQueueItem: ESI.NET.Models.Skills.SkillQueueItem
    {
        /// <summary>
        /// 本地时间
        /// </summary>
        public DateTime FinishDateTime { get; set; }
        /// <summary>
        /// 本地时间
        /// </summary>
        public DateTime StartDateTime { get; set; }
        public string SkillName { get; set; }
        public string Status
        {
            get
            {
                if(IsRunning)
                {
                    return "进行中";
                }
                else if(IsWaiting)
                {
                    return "等待中";
                }
                else if(IsFinished)
                {
                    return "已完成";
                }
                else
                {
                    return "已暂停";
                }
            }
        }
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
                    TimeSpan timeSpan = IsRunning ? FinishDateTime - DateTime.Now : FinishDateTime - StartDateTime;
                    if(timeSpan.Days >= 1)
                    {
                        return $"{timeSpan.Days}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
                    }
                    else
                    {
                        return $"{timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
                    }
                }
            }
        }
        public bool IsRunning
        {
            get => !(IsFinished || IsWaiting || IsPause);
        }
        public bool IsFinished { get => FinishDateTime != DateTime.MinValue && FinishDateTime < DateTime.Now; }
        public bool IsWaiting 
        {
            get
            {
                return StartDateTime != DateTime.MinValue && FinishDateTime != DateTime.MinValue && StartDateTime > DateTime.Now;
            }
        }
        public bool IsPause { get => string.IsNullOrEmpty(FinishDate) || string.IsNullOrEmpty(StartDate); }
        public SkillQueueItem() { }
        public SkillQueueItem(ESI.NET.Models.Skills.SkillQueueItem skillQueueItem)
        {
            this.CopyFrom(skillQueueItem);
            FinishDateTime = string.IsNullOrEmpty(FinishDate) ? DateTime.MinValue : DateTime.Parse(FinishDate);
            StartDateTime = string.IsNullOrEmpty(StartDate) ? DateTime.MinValue : DateTime.Parse(StartDate);
        }
    }
}
