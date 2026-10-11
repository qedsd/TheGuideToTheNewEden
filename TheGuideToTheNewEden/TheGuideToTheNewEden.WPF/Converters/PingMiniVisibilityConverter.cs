using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 迷你模式下"这一刻只显示哪一样"的三态可见性（用户明确的口径）：
/// <list type="table">
/// <item><term>未开始</term><description>只显示 ▶ <b>开始</b>（不需要悬停）</description></item>
/// <item><term>运行中 · 鼠标不在窗口内</term><description>只显示 <b>延迟读数</b>（数字 + ms）</description></item>
/// <item><term>运行中 · 鼠标在窗口内</term><description>显示 **悬停操作组**：■ <b>暂停</b>按钮 + <b>窗口透明度滑条</b>（滑条在按钮下方）</description></item>
/// </list>
/// <para>
/// 多值绑定：`[VM 的 IsRunning, 迷你区域 IsMouseOver]`；<c>ConverterParameter</c> 取
/// <see cref="StartRole"/> / <see cref="ReadingRole"/> / <see cref="HoverControlsRole"/>
/// 决定这一项扮演哪个角色（缺省按 <see cref="StartRole"/>）。
/// 三者互斥、且都用 <see cref="Visibility.Collapsed"/>：不显示时**不占位**，
/// 于是"当前显示的那一样"永远在窗口正中（代价是切换瞬间位置会变，这是"一次只显示一样"的必然结果）。
/// </para>
/// <para>
/// 为什么把三态放进**一个**转换器：这三条规则是同一件事的三个分支，分散到多个 Bool→Visibility
/// 转换器里迟早会互相矛盾（比如"运行中"同时让读数和按钮可见）。
/// </para>
/// </summary>
public sealed class PingMiniVisibilityConverter : IMultiValueConverter
{
    /// <summary>角色：未开始时显示的「开始」按钮。</summary>
    public const string StartRole = "start";

    /// <summary>角色：运行中且鼠标不在窗口内时显示的延迟读数。</summary>
    public const string ReadingRole = "reading";

    /// <summary>角色：运行中且鼠标在窗口内时显示的"悬停操作组"（暂停按钮 + 透明度滑条，两者同一条件）。</summary>
    public const string HoverControlsRole = "hover";

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var running = values.Length > 0 && values[0] is true;
        var hover = values.Length > 1 && values[1] is true;

        var role = (parameter as string)?.ToLowerInvariant() ?? StartRole;

        var visible = role switch
        {
            HoverControlsRole => running && hover,  // 运行中 + 鼠标在窗口内 → 暂停按钮 / 透明度滑条
            ReadingRole => running && !hover,       // 运行中 + 鼠标不在窗口内 → 只显示延迟读数
            _ => !running,                          // 未开始 → 只显示开始
        };

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
