using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.DBModels
{
    [SugarTable("WormholePortal")]
    public class WormholePortal
    {
        [SugarColumn(IsPrimaryKey = true, ColumnDescription = "索引，主键")]
        public int Id { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// 高安、低安、00、虫洞等级
        /// 按,分割
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string Destination { get; set; }
        /// <summary>
        /// 高安、低安、00、虫洞等级
        /// 按,分割
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string AppearsIn { get; set; }
        /// <summary>
        /// 最大稳定时间
        /// </summary>
        public float Lifetime { get; set; }
        /// <summary>
        /// 最大跳跃质量
        /// </summary>
        public long MaxMassPerJump { get; set; }
        /// <summary>
        /// 最大跳跃质量备注
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string MaxMassPerJumpNote { get; set; }
        /// <summary>
        /// 最大稳定质量
        /// </summary>
        public long TotalJumpMass { get; set; }
        /// <summary>
        /// 最大稳定质量备注
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string TotalJumpMassNote { get; set; }
        /// <summary>
        /// 重生：固定、随机
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public string Respawn { get; set; }
        /// <summary>
        /// 再生质量
        /// </summary>
        public long MassRegen { get; set; }
    }
}
