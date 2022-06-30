using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Character
{
    public class SkillQueue
    {
        public DateTime Finish_date { get; set; }
        public int Finished_level { get; set; }
        public int Level_end_sp { get; set; }
        public int Level_start_sp { get; set; }
        public int Queue_position { get; set; }
        public int Skill_id { get; set; }
        public string Skill_name { get; set; }
        public DateTime Start_date { get; set; }
        public int Training_start_sp { get; set; }
        public string Skill_des { get; set; }
        /// <summary>
        /// <0 已完成 =0 暂停 >0 训练队列中 
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                if(Finish_date == DateTime.MinValue)
                {
                    return TimeSpan.FromTicks(0);
                }
                else
                {
                    return TimeSpan.FromTicks((Finish_date - DateTime.UtcNow).Ticks);
                }
            }
        }
        public bool IsTraing { get => Duration.Ticks > 0; }
        public bool IsDone { get => Duration.Ticks < 0; }
        public bool IsPause { get => Duration.Ticks == 0; }
    }
}
