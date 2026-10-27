using System.Windows;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// Primary / Danger 按钮的图标默认颜色。
/// <para>
/// WPF-UI 的 Primary/Danger 样式只改按钮文字与背景，<see cref="SymbolIcon"/> 的 Foreground
/// 不会跟着变（深色主题下停在默认黑色）。这里给所有 <see cref="Wpf.Ui.Controls.Button"/> 挂一个
/// Loaded 类处理器：按钮是 Primary/Danger 且 Icon 是 SymbolIcon 时，统一把图标前景设为
/// TextOnAccentFillColorPrimaryBrush。新增按钮不必再逐个写 Foreground。
/// </para>
/// <para>
/// 为什么不在 <c>GlobalControlStyles.xaml</c> 里再写一个隐式 SymbolIcon 样式：
/// WPF-UI 的 <c>ControlsDictionary</c> 已经带了 <c>{x:Type ui:SymbolIcon}</c> 这个隐式样式键，
/// 再合并一个同键样式会在启动时抛 "Item has already been added"（实测）。
/// 显式写在 SymbolIcon 上的 Foreground 仍然优先（ReadLocalValue 已有时不覆盖）。
/// </para>
/// </summary>
internal static class ButtonIconThemeBehavior
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        EventManager.RegisterClassHandler(
            typeof(Wpf.Ui.Controls.Button),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnButtonLoaded));
    }

    private static void OnButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button button
            || button.Icon is not SymbolIcon icon)
        {
            return;
        }

        if (button.Appearance != ControlAppearance.Primary
            && button.Appearance != ControlAppearance.Danger)
        {
            return;
        }

        // 显式写在图标上的颜色优先，不覆盖
        if (icon.ReadLocalValue(IconElement.ForegroundProperty) != DependencyProperty.UnsetValue)
        {
            return;
        }

        icon.SetResourceReference(
            IconElement.ForegroundProperty,
            "TextOnAccentFillColorPrimaryBrush");
    }
}
