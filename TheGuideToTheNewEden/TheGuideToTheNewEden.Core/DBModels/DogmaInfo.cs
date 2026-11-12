using SqlSugar;

namespace TheGuideToTheNewEden.Core.DBModels
{
    /// <summary>
    /// 主库 <c>dogmaAttributes</c>：属性定义（名称/显示名/单位/分类/默认值/是否"越高越好"）。
    /// <para>主库固定为英文；中文显示名在本地化库 <c>zh.db</c> 的同名表里（见 <see cref="LocalDogmaAttribute"/>）。</para>
    /// </summary>
    [SugarTable("dogmaAttributes")]
    public class DogmaAttribute
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int AttributeID { get; set; }

        public int AttributeCategoryID { get; set; }
        public int DataType { get; set; }
        public double DefaultValue { get; set; }

        /// <summary>内部名（如 <c>maxVelocity</c>）。</summary>
        public string Name { get; set; }

        /// <summary>英文描述。</summary>
        public string Description { get; set; }

        /// <summary>英文显示名（如 <c>Maximum Velocity</c>）。</summary>
        public string DisplayName { get; set; }

        public int IconID { get; set; }
        public bool Published { get; set; }
        public int UnitID { get; set; }
        public bool Stackable { get; set; }

        /// <summary>true = 数值越大越好（游戏里用绿色/白色表达）。</summary>
        public bool HighIsGood { get; set; }
    }

    /// <summary>主库 <c>dogmaUnits</c>：属性单位（m / kg / % / typeID…）。</summary>
    [SugarTable("dogmaUnits")]
    public class DogmaUnit
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int UnitID { get; set; }

        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>主库 <c>dogmaAttributeCategories</c>：属性分组（Fitting / Targeting / Structure…）。</summary>
    [SugarTable("dogmaAttributeCategories")]
    public class DogmaAttributeCategory
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int CategoryID { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
    }

    // ───────────────────────── 本地化库（zh.db）里的同名表 ─────────────────────────
    // 只含"显示名"相关列；主库仍然是英文，查询时以本地库优先、缺了回退主库。

    [SugarTable("dogmaAttributes")]
    public class LocalDogmaAttribute
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int AttributeID { get; set; }

        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    [SugarTable("dogmaUnits")]
    public class LocalDogmaUnit
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int UnitID { get; set; }

        public string DisplayName { get; set; }
    }

    [SugarTable("dogmaAttributeCategories")]
    public class LocalDogmaAttributeCategory
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true)]
        public int CategoryID { get; set; }

        public string Name { get; set; }
    }
}
