namespace TheGuideToTheNewEden.WPF.Models.Tools;

/// <summary>
/// 测速结果的"档位"（延迟与丢包共用）。对应 WinUI 版 <c>ServerPingPage</c> 里那五个阈值段：
/// 延迟 ≤100/≤150/≤250/≤300 与丢包 ≤0/≤1/≤2/≤10。
/// <para>颜色不写在这里——由 <c>PingQualityToBrushConverter</c> 映射到主题语义色，深浅主题自动跟随。</para>
/// </summary>
public enum PingQuality
{
    /// <summary>还没有结果（界面显示 "--"）。</summary>
    Unknown,

    /// <summary>极佳。</summary>
    Excellent,

    /// <summary>良好。</summary>
    Good,

    /// <summary>正常。</summary>
    Normal,

    /// <summary>较差。</summary>
    Poor,

    /// <summary>糟糕。</summary>
    Bad,
}
