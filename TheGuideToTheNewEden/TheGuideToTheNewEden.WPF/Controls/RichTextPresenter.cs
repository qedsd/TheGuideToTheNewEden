using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 只读富文本展示控件：把纯文本渲染成 <see cref="FlowDocument"/>，用户可以**像在网页里那样
/// 任意拖选其中一段**再复制（Ctrl+C / 右键复制都可用）。
/// <para>
/// 为什么需要它：<see cref="TextBlock"/> 根本选不中文本；只读 <see cref="TextBox"/> 虽然能选，
/// 但它是"一个编辑框"——滚动条、光标、右键菜单都是编辑语义，长文排版也只是"一个大段落"。
/// 翻译结果（尤其是整段 MOTD / 邮件正文）更适合按段落排版的富文本。
/// </para>
/// <para>
/// 用法：<c>&lt;controls:RichTextPresenter Text="{Binding ResultText}" /&gt;</c>（只读属性记得写 Mode=OneWay）。
/// 外观（无边框 / 透明底 / 无内边距）在构造函数里给定；字体与前景色由使用方通过
/// <c>FontSize</c>/<c>Foreground</c> 指定（文本靠属性继承着色，控件本身不写死颜色）。
/// </para>
/// </summary>
public class RichTextPresenter : RichTextBox
{
    /// <summary>要展示的纯文本（<c>\n</c> 会变成真正的换行）。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(RichTextPresenter),
        new FrameworkPropertyMetadata(string.Empty, OnTextChanged));

    /// <summary>文本更新后是否自动滚到底部（流式输出时打开，静态展示时保持关闭）。</summary>
    public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.Register(
        nameof(AutoScrollToEnd),
        typeof(bool),
        typeof(RichTextPresenter),
        new PropertyMetadata(false, OnAutoScrollToEndChanged));

    public RichTextPresenter()
    {
        // 只读富文本：能选、能复制，但不能改
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsDocumentEnabled = true;
        IsInactiveSelectionHighlightEnabled = true;
        IsUndoEnabled = false;
        AcceptsTab = false;

        // 外观与普通文本一致（无边框、透明底、无内边距、无焦点虚线框）
        BorderThickness = new Thickness(0);
        BorderBrush = Brushes.Transparent;
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        FocusVisualStyle = null;
        SelectionBrush = TryFindBrush("SystemAccentColorPrimaryBrush");
        SelectionOpacity = 0.4;

        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        Document = BuildDocument(string.Empty);
    }

    /// <summary>要展示的纯文本。</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool AutoScrollToEnd
    {
        get => (bool)GetValue(AutoScrollToEndProperty);
        set => SetValue(AutoScrollToEndProperty, value);
    }

    /// <summary>取当前选中的文本（未选中返回空串）。</summary>
    public string SelectedText => Selection?.Text ?? string.Empty;

    /// <summary>文本是否为空（界面据此显示占位提示）。</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (RichTextPresenter)d;
        presenter.Document = BuildDocument(e.NewValue as string);
        if (presenter.AutoScrollToEnd)
        {
            presenter.ScrollToEnd();
        }
    }

    private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ((RichTextPresenter)d).ScrollToEnd();
        }
    }

    /// <summary>把纯文本变成"一行一段"的文档（用 <see cref="LineBreak"/> 保留空行，避免段落间距）。</summary>
    private static FlowDocument BuildDocument(string? text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }

            paragraph.Inlines.Add(new Run(lines[i]));
        }

        var document = new FlowDocument(paragraph)
        {
            PagePadding = new Thickness(0),
            LineHeight = double.NaN,
        };

        return document;
    }

    /// <summary>构造期取一次主题色画笔（取不到就用 null，交给 WPF 默认选区色）。</summary>
    private static Brush? TryFindBrush(string key)
    {
        try
        {
            return Application.Current?.TryFindResource(key) as Brush;
        }
        catch
        {
            return null;
        }
    }
}
