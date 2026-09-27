using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 通用颜色选择器（软件级复用）：A/R/G/B 滑杆与数值框双向联动 + 十六进制输入（#RGB/#RRGGBB/#AARRGGBB）+ 预览。
/// 用法：<c>SetColor</c> 初始化；订阅 <see cref="ColorChanged"/> 实时取值；或读 <see cref="SelectedColor"/>。
/// </summary>
public partial class ColorPickerView : UserControl
{
    private bool _updating;   // 程序化回填（SetColor / 组件互同步）时抑制事件

    public ColorPickerView()
    {
        InitializeComponent();
        SetColor(Colors.Red);
    }

    public static readonly RoutedEvent ColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ColorChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorPickerView));

    /// <summary>任意组件改动导致颜色变化（滑杆拖动 / 数值输入 / HEX 回车确认）。</summary>
    public event RoutedEventHandler ColorChanged
    {
        add => AddHandler(ColorChangedEvent, value);
        remove => RemoveHandler(ColorChangedEvent, value);
    }

    /// <summary>当前颜色（含 Alpha 通道）。</summary>
    public Color SelectedColor => Color.FromArgb(
        (byte)ASlider.Value, (byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value);

    /// <summary>程序化设置颜色（不触发 <see cref="ColorChanged"/>）。</summary>
    public void SetColor(Color color)
    {
        _updating = true;
        try
        {
            ASlider.Value = color.A;
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            ANumber.Value = color.A;
            RNumber.Value = color.R;
            GNumber.Value = color.G;
            BNumber.Value = color.B;
            PreviewBrush.Color = color;
            HexBox.Text = color.A == 255
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        finally
        {
            _updating = false;
        }
    }

    private void Component_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        // 方向感知：滑杆或数值框谁被改动，以它为准，同步另一侧
        var a = ASlider.Value;
        var r = RSlider.Value;
        var g = GSlider.Value;
        var b = BSlider.Value;
        switch (sender)
        {
            case Slider s when ReferenceEquals(s, ASlider): a = s.Value; break;
            case Slider s when ReferenceEquals(s, RSlider): r = s.Value; break;
            case Slider s when ReferenceEquals(s, GSlider): g = s.Value; break;
            case Slider s when ReferenceEquals(s, BSlider): b = s.Value; break;
            case Wpf.Ui.Controls.NumberBox n when ReferenceEquals(n, ANumber): a = n.Value ?? 0; break;
            case Wpf.Ui.Controls.NumberBox n when ReferenceEquals(n, RNumber): r = n.Value ?? 0; break;
            case Wpf.Ui.Controls.NumberBox n when ReferenceEquals(n, GNumber): g = n.Value ?? 0; break;
            case Wpf.Ui.Controls.NumberBox n when ReferenceEquals(n, BNumber): b = n.Value ?? 0; break;
        }

        _updating = true;
        try
        {
            ASlider.Value = a;
            RSlider.Value = r;
            GSlider.Value = g;
            BSlider.Value = b;
            ANumber.Value = a;
            RNumber.Value = r;
            GNumber.Value = g;
            BNumber.Value = b;
        }
        finally
        {
            _updating = false;
        }

        ApplyComponents();
    }

    private void ApplyComponents()
    {
        var color = SelectedColor;
        PreviewBrush.Color = color;
        HexBox.Text = color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        RaiseEvent(new RoutedEventArgs(ColorChangedEvent, this));
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHex();
        }
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (_updating)
        {
            return;
        }

        try
        {
            SetColor((Color)ColorConverter.ConvertFromString(HexBox.Text.Trim()));
            RaiseEvent(new RoutedEventArgs(ColorChangedEvent, this));
        }
        catch (FormatException)
        {
            // 非法串：回填为当前颜色，不打断输入
            SetColor(SelectedColor);
        }
        catch (NullReferenceException)
        {
            SetColor(SelectedColor);
        }
    }
}
