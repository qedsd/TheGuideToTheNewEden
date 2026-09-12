using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 全屏等待遮罩（对应 WinUI 版 <c>ShowWaiting</c>）：半透明背景 + 旋转指示 + 文案 + 可选"取消"按钮。
/// 由 <see cref="Services.PageNotifyService"/> 统一驱动，放在 MainWindow 里全局复用。
/// </summary>
public partial class WaitingOverlay : UserControl
{
    private Action? _cancelAction;

    public WaitingOverlay()
    {
        InitializeComponent();
    }

    /// <summary>显示等待态。<paramref name="cancelAction"/> 非空时显示"取消"按钮。</summary>
    public void Show(string message, Action? cancelAction)
    {
        _cancelAction = cancelAction;
        MessageText.Text = message;
        CancelButton.IsEnabled = true;
        CancelButton.Visibility = cancelAction is null ? Visibility.Collapsed : Visibility.Visible;
        Visibility = Visibility.Visible;
        StartSpin();
    }

    /// <summary>更新等待文案（例如"获取源市场订单中:123/411"）。</summary>
    public void UpdateText(string message)
    {
        if (Visibility == Visibility.Visible)
        {
            MessageText.Text = message;
        }
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _cancelAction = null;
        SpinRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        // 取消后先禁用按钮，避免重复触发；下次 Show 时重新启用
        CancelButton.IsEnabled = false;
        _cancelAction?.Invoke();
    }

    private void StartSpin()
    {
        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        SpinRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
    }
}
