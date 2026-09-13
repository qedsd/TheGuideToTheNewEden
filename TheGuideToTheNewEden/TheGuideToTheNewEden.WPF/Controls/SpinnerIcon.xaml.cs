using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 行内加载指示（转圈的箭头图标）：<see cref="IsActive"/> 为 true 时显示并一直转，false 时停止、归零并**自身隐藏**
/// （所以放在布局里不会白占位置，也不会留下一个静止的图标）。
/// <para>
/// 用在"某一条译文正在生成"这种**局部**等待上——不占用全局等待遮罩，用户可以继续发下一条。
/// </para>
/// </summary>
public partial class SpinnerIcon : UserControl
{
    /// <summary>是否正在转。</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(SpinnerIcon),
        new PropertyMetadata(false, OnIsActiveChanged));

    public SpinnerIcon()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var spinner = (SpinnerIcon)d;
        if (e.NewValue is true)
        {
            spinner.Start();
        }
        else
        {
            spinner.Stop();
        }
    }

    private void Start()
    {
        Visibility = Visibility.Visible;

        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        SpinRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void Stop()
    {
        SpinRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        SpinRotate.Angle = 0;
        Visibility = Visibility.Collapsed;
    }
}
