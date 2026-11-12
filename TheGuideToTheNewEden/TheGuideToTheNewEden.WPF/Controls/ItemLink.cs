using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 让任意文字元素（通常是 <see cref="TextBlock"/>）变成"物品链接"的附加行为（阶段 282/283）：
/// 设置 <c>controls:ItemLink.TypeId="{Binding TypeId}"</c> 后，鼠标变成手型、悬停加下划线，
/// **左键打开「物品详情」窗口**（阶段 283 起取代原来的"直接跳市场"，窗口底部有跳市场按钮）。
/// <para>
/// 用附加行为而不是换控件，是为了不动各处的排版（字号/字重/裁剪/换行），
/// 制造业界面里所有"物品名称"都能一行 XAML 接上跳转。
/// </para>
/// </summary>
public static class ItemLink
{
    public static readonly DependencyProperty TypeIdProperty = DependencyProperty.RegisterAttached(
        "TypeId",
        typeof(long),
        typeof(ItemLink),
        new PropertyMetadata(0L, OnTypeIdChanged));

    /// <summary>物品 TypeID；&lt;=0 时自动退化成普通文字（不挂事件、不变手型）。</summary>
    public static void SetTypeId(DependencyObject element, long value) => element.SetValue(TypeIdProperty, value);

    public static long GetTypeId(DependencyObject element) => (long)element.GetValue(TypeIdProperty);

    private static readonly Dictionary<TextBlock, TextDecorationCollection?> OriginalDecorations = new();

    private static void OnTypeIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        // 先摘掉旧挂载，避免同一元素重复订阅
        element.MouseLeftButtonUp -= OnClick;
        element.MouseEnter -= OnEnter;
        element.MouseLeave -= OnLeave;

        if (GetTypeId(element) <= 0)
        {
            element.Cursor = null;
            return;
        }

        element.Cursor = Cursors.Hand;
        element.MouseLeftButtonUp += OnClick;
        element.MouseEnter += OnEnter;
        element.MouseLeave += OnLeave;
    }

    private static void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        var typeId = GetTypeId(element);
        if (typeId <= 0 || typeId > int.MaxValue)
        {
            return;
        }

        // 标记已处理：不再让外层（列表项 / 整卡）把它当成选中或打开
        e.Handled = true;
        ItemDetailWindow.ShowItem((int)typeId);
    }

    /// <summary>悬停加下划线（不动颜色，避免覆盖各处的 DynamicResource 前景色）。</summary>
    private static void OnEnter(object sender, MouseEventArgs e)
    {
        if (sender is not TextBlock text)
        {
            return;
        }

        OriginalDecorations[text] = text.TextDecorations;
        text.TextDecorations = TextDecorations.Underline;
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is not TextBlock text)
        {
            return;
        }

        if (OriginalDecorations.TryGetValue(text, out var decorations))
        {
            text.TextDecorations = decorations;
            OriginalDecorations.Remove(text);
        }
    }
}
