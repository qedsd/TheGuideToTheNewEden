using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 只读 **Markdown** 展示控件：把 Markdown 渲染成 <see cref="FlowDocument"/>（见 <see cref="MarkdownRenderer"/>），
/// 用户可以像在网页里那样任意拖选一段再复制（Ctrl+C / 右键复制都可用），也能滚动看长文。
/// <para>
/// 与 <see cref="RichTextPresenter"/> 的关系：那个吃**纯文本**（翻译结果 / 邮件正文），这个吃 **Markdown**
/// （DED 攻略、任务攻略这类从 wiki 导出的正文）。两者的只读/可选/滚动/外观设置保持一致。
/// </para>
/// <para>
/// 用法：<c>&lt;controls:MarkdownPresenter Markdown="{Binding Selected.Content, Mode=OneWay}" /&gt;</c>
/// ——只读属性务必写 <c>Mode=OneWay</c>（<c>TextBox.Text</c> 那种默认 TwoWay 的坑见 REFACTORING.md §9 第 27 条）。
/// 字体与前景色由使用方通过 <c>FontSize</c>/<c>Foreground</c> 指定（文本靠属性继承着色，控件不写死颜色）。
/// </para>
/// </summary>
public class MarkdownPresenter : RichTextBox
{
    /// <summary>要展示的 Markdown 文本。</summary>
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownPresenter),
        new FrameworkPropertyMetadata(string.Empty, OnMarkdownChanged));

    /// <summary>
    /// 正文字号（DIP）。默认 14 = 全应用基础字号；**攻略页这种长文阅读面板调到 16 更舒服**。
    /// <para>
    /// 单独开一个属性而不是复用 <see cref="System.Windows.Controls.Control.FontSize"/>：
    /// <c>RichTextBox</c> 的文档不从这个属性继承（§9 第 18 条的同款坑），改了也不生效；
    /// 而用独立属性可以明确"这是给渲染器用的基准字号"，不会让调用方误以为设 FontSize 就行了。
    /// </para>
    /// </summary>
    public static readonly DependencyProperty BaseFontSizeProperty = DependencyProperty.Register(
        nameof(BaseFontSize),
        typeof(double),
        typeof(MarkdownPresenter),
        new FrameworkPropertyMetadata(14.0, OnBaseFontSizeChanged));

    public MarkdownPresenter()
    {
        // 只读富文本：能选、能复制，但不能改
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsDocumentEnabled = true;   // Hyperlink 要能被点击
        IsInactiveSelectionHighlightEnabled = true;
        IsUndoEnabled = false;
        AcceptsTab = false;

        // 外观与普通文本一致（无边框、透明底、无内边距、无焦点虚线框）
        BorderThickness = new Thickness(0);
        BorderBrush = Brushes.Transparent;
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        FocusVisualStyle = null;
        SelectionOpacity = 0.4;

        // 选区色走资源引用（不在构造期缓存 Brush 实例——主题/强调色变了要跟着变，见 §9 第 18 条）
        SetResourceReference(SelectionBrushProperty, "SystemAccentColorPrimaryBrush");

        // 默认前景色 = 主题正文色。**不能靠继承**：页面/窗口根节点不一定会设 Foreground，
        // 不设就退回系统黑（深色主题下就是"黑字黑底"，§9 第 61 条的同款坑）。
        // 调用方在 XAML 里显式写 Foreground 仍会覆盖这里的默认值。
        SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");

        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        Document = Render(null);
    }

    /// <summary>要展示的 Markdown 文本。</summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>正文字号（DIP），标题与代码块按比例跟随。默认 14。</summary>
    public double BaseFontSize
    {
        get => (double)GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    /// <summary>取当前选中的文本（未选中返回空串）。</summary>
    public string SelectedText => Selection?.Text ?? string.Empty;

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (MarkdownPresenter)d;
        presenter.Document = presenter.Render(e.NewValue as string);
    }

    private static void OnBaseFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 标题字号是在渲染时算进来的，所以改字号必须重渲染（不是改个属性就能生效）
        var presenter = (MarkdownPresenter)d;
        presenter.Document = presenter.Render(presenter.Markdown);
    }

    /// <summary>
    /// 渲染正文，并把**文档自己的**默认前景色设为主题正文色——这样即使从控件继承的链路不生效，
    /// 段落与 Run 也会继承到这个主题色（页面上显式设色的部分，如引用块/超链接，仍然各自覆盖）。
    /// </summary>
    private FlowDocument Render(string? markdown)
    {
        var document = MarkdownRenderer.ToFlowDocument(markdown, BaseFontSize);
        document.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        return document;
    }
}
