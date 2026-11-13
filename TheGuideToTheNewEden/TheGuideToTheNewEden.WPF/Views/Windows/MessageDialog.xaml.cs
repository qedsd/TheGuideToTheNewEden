using System.Windows;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 统一风格的消息对话框（阶段 320）：替换散落各处的 WPF 原生 <c>MessageBox</c>。
/// <para>
/// <see cref="Show"/> 重载与 <see cref="System.Windows.MessageBox"/> 的常用签名一一对应，
/// 返回 <see cref="System.Windows.MessageBoxResult"/>——调用方把 <c>MessageBox.Show(</c> 换成
/// <c>MessageDialog.Show(</c> 即可，后续判断逻辑不用动。
/// </para>
/// </summary>
public partial class MessageDialog : FluentWindow
{
    private System.Windows.MessageBoxResult _result = System.Windows.MessageBoxResult.None;

    private MessageDialog()
    {
        InitializeComponent();
        // 阶段 321b：显示时机再套一次高度（个别环境模板/宿主可能在 Show 阶段改写）
        SourceInitialized += (_, _) => ApplyHeight();
    }

    /// <summary>消息内容。</summary>
    private string Message { get; init; } = string.Empty;

    /// <summary>标题（null 时用应用名）。</summary>
    private string Caption { get; init; } = string.Empty;

    private System.Windows.MessageBoxButton Buttons { get; init; } = System.Windows.MessageBoxButton.OK;

    private System.Windows.MessageBoxImage Icon { get; init; } = System.Windows.MessageBoxImage.None;

    public static System.Windows.MessageBoxResult Show(string messageBoxText)
        => ShowCore(null, messageBoxText, null, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None);

    public static System.Windows.MessageBoxResult Show(string messageBoxText, string? caption)
        => ShowCore(null, messageBoxText, caption, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.None);

    public static System.Windows.MessageBoxResult Show(string messageBoxText, string? caption, System.Windows.MessageBoxButton button)
        => ShowCore(null, messageBoxText, caption, button, System.Windows.MessageBoxImage.None);

    public static System.Windows.MessageBoxResult Show(string messageBoxText, string? caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        => ShowCore(null, messageBoxText, caption, button, icon);

    public static System.Windows.MessageBoxResult Show(Window? owner, string messageBoxText, string? caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon)
        => ShowCore(owner, messageBoxText, caption, button, icon);

    public static System.Windows.MessageBoxResult Show(string messageBoxText, string? caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon, System.Windows.MessageBoxResult defaultResult)
        => ShowCore(null, messageBoxText, caption, button, icon);

    public static System.Windows.MessageBoxResult Show(System.Windows.Window? owner, string messageBoxText, string? caption, System.Windows.MessageBoxButton button, System.Windows.MessageBoxImage icon, System.Windows.MessageBoxResult defaultResult)
        => ShowCore(owner, messageBoxText, caption, button, icon);

    private static System.Windows.MessageBoxResult ShowCore(
        Window? owner,
        string messageBoxText,
        string? caption,
        System.Windows.MessageBoxButton button,
        System.Windows.MessageBoxImage icon)
    {
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        var dialog = new MessageDialog
        {
            Message = messageBoxText ?? string.Empty,
            Caption = string.IsNullOrWhiteSpace(caption)
                ? Application.Current?.TryFindResource("AppDisplayName") as string ?? string.Empty
                : caption,
            Buttons = button,
            Icon = icon,
        };
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.Prepare();
        dialog.ShowDialog();
        return dialog._result;
    }

    private void Prepare()
    {
        TitleBar.Title = Caption;
        Title = Caption;
        TitleText.Text = Caption;
        MessageText.Text = Message;

        // 图标：Question 紫问号 / Error 红 ✕ / Warning 黄 ⚠ / Information 蓝 ⓘ
        var (symbol, glyph) = Icon switch
        {
            System.Windows.MessageBoxImage.Error => (SymbolRegular.DismissCircle24, "SystemFillColorCriticalBrush"),
            System.Windows.MessageBoxImage.Warning => (SymbolRegular.Warning24, "SystemFillColorCautionBrush"),
            System.Windows.MessageBoxImage.Question => (SymbolRegular.QuestionCircle24, "SystemFillColorSuccessBrush"),
            _ => (SymbolRegular.Info24, "SystemFillColorInfoBrush"),
        };
        IconSymbol.Symbol = symbol;
        if (Application.Current?.TryFindResource(glyph) is System.Windows.Media.Brush brush)
        {
            IconSymbol.Foreground = brush;
        }

        ApplyHeight();

        switch (Buttons)
        {
            case System.Windows.MessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                _result = System.Windows.MessageBoxResult.No;
                break;
            case System.Windows.MessageBoxButton.OKCancel:
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                _result = System.Windows.MessageBoxResult.Cancel;
                break;
            default:
                OkButton.Visibility = Visibility.Visible;
                _result = System.Windows.MessageBoxResult.OK;
                break;
        }
    }

    /// <summary>阶段 321/321b：按消息文本行数精确计算窗口高度（SizeToContent 在 FluentWindow 下不生效）。</summary>
    private void ApplyHeight()
    {
        var typeface = new System.Windows.Media.Typeface(
            MessageText.FontFamily, System.Windows.FontStyles.Normal, MessageText.FontWeight, System.Windows.FontStretches.Normal);
        var formatted = new System.Windows.Media.FormattedText(
            Message ?? string.Empty,
            System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            MessageText.FontSize,
            System.Windows.Media.Brushes.Black,
            System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
        const double textWidth = 408; // 440 - 左右边距 16*2
        var lines = Math.Max(1, (int)Math.Ceiling(formatted.Width / textWidth));
        var messageHeight = lines * formatted.LineHeight * 1.4;
        var height = Math.Clamp(messageHeight + 160, 170, 560);
        // 阶段 321c：Wpf.Ui 的 ControlsDictionary 隐式样式把 FluentWindow 的 MinHeight 定为 320，
        // 只设 Height 会被钳回 320（用户端表现为"窗口固定 400 物理像素"）——
        // 本地 MinHeight/MaxHeight 一并覆盖隐式样式，高度才真正生效。
        MinHeight = height;
        MaxHeight = height + 40;
        Height = height;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _result = System.Windows.MessageBoxResult.OK;
        Close();
    }

    private void OnYesClick(object sender, RoutedEventArgs e)
    {
        _result = System.Windows.MessageBoxResult.Yes;
        Close();
    }

    private void OnNoClick(object sender, RoutedEventArgs e)
    {
        _result = System.Windows.MessageBoxResult.No;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _result = System.Windows.MessageBoxResult.Cancel;
        Close();
    }
}
