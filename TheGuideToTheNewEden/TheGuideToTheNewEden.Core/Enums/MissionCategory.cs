using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Enums
{
    /// <summary>
    /// 任务分类（与 wiki 的分类一一对应，顺序即界面上分类下拉的顺序）。
    /// <para>
    /// 取值**不要随便改**：数据库里存的就是这些整数，改了等于把旧库读错。
    /// </para>
    /// </summary>
    public enum MissionCategory
    {
        /// <summary>安全任务 / Encounter（含战斗、回收、侦察）。</summary>
        Security = 0,

        /// <summary>运输任务 / Courier（也叫 Distribution）。</summary>
        Distribution = 1,

        /// <summary>采矿任务（要采任务矿石交回去）。</summary>
        Mining = 2,

        /// <summary>故事线任务（每 16 个普通任务给一次）。</summary>
        Storyline = 3,

        /// <summary>史诗任务线（Epic Arc）里的单个任务。</summary>
        Epic = 4,

        /// <summary>异常任务 / Anomic（俗称燃烧者 Burner）。</summary>
        Anomic = 5,

        /// <summary>COSMOS 任务。</summary>
        Cosmos = 6,

        /// <summary>研发任务（R&amp;D，给研究点换数据核心）。</summary>
        Rnd = 7,

        /// <summary>职业代理人任务（新手教程链）。</summary>
        Career = 8,

        /// <summary>Circle 任务（海盗势力圈的任务线）。</summary>
        Circle = 9,
    }
}
