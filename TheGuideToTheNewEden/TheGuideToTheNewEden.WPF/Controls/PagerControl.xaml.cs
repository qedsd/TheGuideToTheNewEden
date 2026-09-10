using System.Windows;
using System.Windows.Controls;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 通用分页控件：页码 + 上一页/下一页。
/// ESI 的多数列表接口只能按页取，无法预知总页数，因此用"是否还有下一页"控制。
/// </summary>
public partial class PagerControl : UserControl
{
    public PagerControl()
    {
        InitializeComponent();

        PrevButton.Click += (_, _) => Raise(Page - 1);
        NextButton.Click += (_, _) => Raise(Page + 1);
        UpdateVisual();
    }

    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(
        nameof(Page), typeof(int), typeof(PagerControl), new PropertyMetadata(1, OnStateChanged));

    public static readonly DependencyProperty HasNextProperty = DependencyProperty.Register(
        nameof(HasNext), typeof(bool), typeof(PagerControl), new PropertyMetadata(false, OnStateChanged));

    public int Page
    {
        get => (int)GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public bool HasNext
    {
        get => (bool)GetValue(HasNextProperty);
        set => SetValue(HasNextProperty, value);
    }

    public event EventHandler<int>? PageChanged;

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PagerControl)d).UpdateVisual();
    }

    private void Raise(int page)
    {
        if (page < 1 || page == Page)
        {
            return;
        }

        PageChanged?.Invoke(this, page);
    }

    private void UpdateVisual()
    {
        if (!IsInitialized)
        {
            return;
        }

        PageText.Text = string.Format(
            Application.Current?.TryFindResource("Characters.Page") as string ?? "Page {0}",
            Page);
        PrevButton.IsEnabled = Page > 1;
        NextButton.IsEnabled = HasNext;
    }
}