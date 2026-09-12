using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 通用卡片容器（对齐 WinUI 版 <c>Controls/CardControl</c>）：统一"标题 / 内容 / 底栏"的
/// 卡片式布局与背景描边，三个区域都可放<b>任意 UI</b>，只负责整体布局：
/// <list type="bullet">
///   <item><b>Header</b>：标题行（如标题文字、图标+文字）；未设置则不占空间；</item>
///   <item><b>Content</b>：主内容区（继承自 <see cref="ContentControl.Content"/>，默认内容属性）；自适应填满剩余高度；</item>
///   <item><b>Footer</b>：底栏（通常放操作按钮），上方带一条强调色分隔线；未设置则不占空间。</item>
/// </list>
/// 用法：<c>&lt;controls:CardControl&gt;&lt;controls:CardControl.Footer&gt;…&lt;/controls:CardControl.Footer&gt;主内容&lt;/controls:CardControl&gt;</c>。
/// 外观由 App 资源中的隐式样式 <c>Controls/CardControlStyles.xaml</c> 提供
/// （圆角卡片背景 + 描边；可整体覆盖 Background/BorderBrush/CornerRadius/Padding）。
/// </summary>
public class CardControl : ContentControl
{
    /// <summary>标题区内容（任意 UI，常用标题文字；为空则不显示标题行）。</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(CardControl), new PropertyMetadata(null));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>标题区模板。</summary>
    public static readonly DependencyProperty HeaderTemplateProperty = DependencyProperty.Register(
        nameof(HeaderTemplate), typeof(DataTemplate), typeof(CardControl), new PropertyMetadata(null));

    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>底栏内容（任意 UI，常用操作按钮；为空则不显示分隔线与底栏）。</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(CardControl), new PropertyMetadata(null));

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>底栏模板。</summary>
    public static readonly DependencyProperty FooterTemplateProperty = DependencyProperty.Register(
        nameof(FooterTemplate), typeof(DataTemplate), typeof(CardControl), new PropertyMetadata(null));

    public DataTemplate? FooterTemplate
    {
        get => (DataTemplate?)GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    /// <summary>卡片圆角（默认样式给 6）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(CardControl), new PropertyMetadata(new CornerRadius(6)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>标题区与内容区之间的分隔线是否显示（默认显示）。</summary>
    public static readonly DependencyProperty ShowHeaderSeparatorProperty = DependencyProperty.Register(
        nameof(ShowHeaderSeparator), typeof(bool), typeof(CardControl), new PropertyMetadata(true));

    public bool ShowHeaderSeparator
    {
        get => (bool)GetValue(ShowHeaderSeparatorProperty);
        set => SetValue(ShowHeaderSeparatorProperty, value);
    }
}
