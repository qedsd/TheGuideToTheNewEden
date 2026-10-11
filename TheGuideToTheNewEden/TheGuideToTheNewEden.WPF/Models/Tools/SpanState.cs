namespace TheGuideToTheNewEden.WPF.Models.Tools;

/// <summary>
/// 键盘"跨度"测速的运行状态（决定"通过次数"的显示颜色）。
/// </summary>
public enum SpanState
{
    /// <summary>未在计时/未开始。</summary>
    Idle,

    /// <summary>正在计时或计数中。</summary>
    Counting,

    /// <summary>已达到目标次数。</summary>
    Reached,
}
