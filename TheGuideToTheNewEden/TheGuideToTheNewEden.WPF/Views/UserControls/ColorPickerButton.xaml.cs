using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using DrawingColor = System.Drawing.Color;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 颜色按钮：显示 色块 + #AARRGGBB，点开弹层复用 <see cref="ColorPickerView"/>（阶段 135）。
/// <para>
/// 颜色值直接使用 Core 的 <see cref="System.Drawing.Color"/>，因此可以
/// <c>Color="{Binding SelectedSetting.HighlightColor, Mode=TwoWay}"</c> 绑定到 <c>PreviewItem</c>，
/// 拖动滑杆时逐帧回写设置（调用方据此实时套用到预览窗口），无需额外的转换器。
/// </para>
/// </summary>
public partial class ColorPickerButton : UserControl
{
    /// <summary>程序化回填（弹层 → 依赖属性 / 依赖属性 → 弹层）时抑制事件，避免互相触发。</summary>
    private bool _syncing;

    public ColorPickerButton()
    {
        InitializeComponent();

        // 切页签 / 切换选中客户端时控件会被卸载或换绑，弹层必须跟着关掉，否则会浮在别的页面上
        Unloaded += (_, _) => PickerPopup.IsOpen = false;
        DataContextChanged += (_, _) => PickerPopup.IsOpen = false;
    }

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(DrawingColor),
        typeof(ColorPickerButton),
        new FrameworkPropertyMetadata(
            DrawingColor.Green,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnColorPropertyChanged));

    /// <summary>当前颜色（含 Alpha 通道）。</summary>
    public DrawingColor Color
    {
        get => (DrawingColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 外部（绑定源 / 其它设置项复制）改了颜色：弹层正开着就同步过去
        if (d is ColorPickerButton button)
        {
            button.SyncPickerFromValue();
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        _syncing = true;
        try
        {
            Picker.SetColor(PreviewColorHelper.ToMediaColor(Color));
        }
        finally
        {
            _syncing = false;
        }

        PickerPopup.IsOpen = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => PickerPopup.IsOpen = false;

    private void OnPickerColorChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        var color = Picker.SelectedColor;
        _syncing = true;
        try
        {
            // SetCurrentValue：保留 XAML 里的 TwoWay 绑定（SetValue 会把绑定顶掉）
            SetCurrentValue(ColorProperty, DrawingColor.FromArgb(color.A, color.R, color.G, color.B));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncPickerFromValue()
    {
        if (_syncing || !PickerPopup.IsOpen)
        {
            return;
        }

        _syncing = true;
        try
        {
            Picker.SetColor(PreviewColorHelper.ToMediaColor(Color));
        }
        finally
        {
            _syncing = false;
        }
    }
}
