using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>
/// 主页上的一条快捷方式（<c>Configs/HomeShortcuts.json</c> 的一项）。
/// <para>
/// <see cref="Id"/> 是<b>稳定的 kebab-case 字符串</b>（如 <c>channel-intel</c>），指向
/// <see cref="HomeFeatureCatalog"/> 里的一个功能；刻意不用页面类型名，这样功能改名/换页时
/// 用户已固定的磁贴不会失效，用户也可以手工编辑这个文件。
/// </para>
/// </summary>
public sealed class HomeShortcutItem
{
    /// <summary>小磁贴（112 × 112，默认）。</summary>
    public const string SmallSize = "small";

    /// <summary>宽磁贴（232 × 112，占两个小磁贴的宽度）。</summary>
    public const string WideSize = "wide";

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>磁贴尺寸；缺省（null）等于 <see cref="SmallSize"/>。</summary>
    [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
    public string? Size { get; set; }

    /// <summary>是否显示状态角标；缺省（null）表示"由功能自己的运行状态决定"（P2 使用）。</summary>
    [JsonProperty("badge", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Badge { get; set; }

    /// <summary>功能参数（P3：物品 / 角色 / 星系的 ID 文本）。</summary>
    [JsonProperty("param", NullValueHandling = NullValueHandling.Ignore)]
    public string? Param { get; set; }

    /// <summary>
    /// 参数化磁贴的显示名（P3，例如物品名"因卡萨斯级"）。
    /// <b>刻意在创建时写死到文件里</b>：主页渲染磁贴是同步过程，不该为了一个标题去做
    /// 名称解析（SDE 查询 / 结构名 ESI 调用）。名字变了也不影响点开后的正确性。
    /// </summary>
    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonIgnore]
    public bool IsWide => string.Equals(Size, WideSize, StringComparison.OrdinalIgnoreCase);

    /// <summary>磁贴的唯一键：同一功能可以有多个不同参数的磁贴。</summary>
    [JsonIgnore]
    public string Key => string.IsNullOrEmpty(Param) ? Id : Id + "#" + Param;

    public HomeShortcutItem Clone() => new()
    {
        Id = Id,
        Size = Size,
        Badge = Badge,
        Param = Param,
        Label = Label,
    };
}

/// <summary><c>HomeShortcuts.json</c> 的根对象。</summary>
public sealed class HomeShortcutFile
{
    /// <summary>固定磁贴数量上限，超出后主页的「＋ 添加功能」会禁用。</summary>
    public const int DefaultMaxItems = 24;

    /// <summary>「最近使用」最多记录多少个（开关关闭时不记录，见 <see cref="ShowRecent"/>）。</summary>
    public const int DefaultRecentCapacity = 8;

    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    [JsonProperty("maxItems")]
    public int MaxItems { get; set; } = DefaultMaxItems;

    /// <summary>数组顺序 = 主页上的磁贴顺序。</summary>
    [JsonProperty("items")]
    public List<HomeShortcutItem> Items { get; set; } = [];

    // ---------- 显示策略（P1 / P2） ----------

    /// <summary>钱包卡区间：7 = 近 7 天、30 = 近 30 天、0 = 全部。</summary>
    [JsonProperty("walletRangeDays")]
    public int WalletRangeDays { get; set; } = 7;

    [JsonProperty("showWalletCard")]
    public bool ShowWalletCard { get; set; } = true;

    [JsonProperty("showMarketCard")]
    public bool ShowMarketCard { get; set; } = true;

    /// <summary>是否显示「最近使用」条（默认关：不记录也不占位置）。</summary>
    [JsonProperty("showRecent")]
    public bool ShowRecent { get; set; }

    // showGrouped 字段已随阶段 101 移除（「磁贴按分组分段显示」功能取消）：
    // System.Text.Json 反序列化默认忽略未知成员，旧配置文件里的该字段不会报错，
    // 下次保存时自然消失。

    /// <summary>最近打开过的功能 id（最新在前，容量 <see cref="DefaultRecentCapacity"/>）。</summary>
    [JsonProperty("recent")]
    public List<string> Recent { get; set; } = [];
}
