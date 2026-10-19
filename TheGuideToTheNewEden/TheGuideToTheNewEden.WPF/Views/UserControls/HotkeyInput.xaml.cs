using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 快捷键输入：可直接输入，也可打开 <see cref="HotkeyPickerWindow"/>（左侧按键列表 + 右侧模拟键盘 + 录制）。
/// </summary>
public partial class HotkeyInput : UserControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey),
        typeof(string),
        typeof(HotkeyInput),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            null,
            null,
            true,
            UpdateSourceTrigger.PropertyChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(HotkeyInput),
        new PropertyMetadata(string.Empty));

    public HotkeyInput()
    {
        InitializeComponent();
    }

    /// <summary>快捷键字符串，空字符串表示未设置。</summary>
    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>输入框失焦或选择弹窗点"确定/清除"后触发；页面可借此立即保存/刷新预览。</summary>
    public event EventHandler? HotkeyApplied;

    private void OnOpenKeyboardClick(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyPickerWindow { Hotkey = Hotkey };
        if (Window.GetWindow(this) is { } owner)
        {
            dialog.Owner = owner;
        }

        if (dialog.ShowDialog() == true)
        {
            SetCurrentValue(HotkeyProperty, dialog.SelectedHotkey);
            CommitHotkey();
        }
    }

    private void OnInputLostFocus(object sender, RoutedEventArgs e)
    {
        // LostFocus 绑定先把 TextBox 文本推回 Hotkey；这里再通知页面"已经改完"
        InputBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        CommitHotkey();
    }

    /// <summary>
    /// SetCurrentValue 只改当前值、不破坏宿主绑定；再显式 UpdateSource，
    /// 保证全局热键/分组热键/单客户端热键的 VM 立即拿到新字符串。
    /// </summary>
    private void CommitHotkey()
    {
        GetBindingExpression(HotkeyProperty)?.UpdateSource();
        HotkeyApplied?.Invoke(this, EventArgs.Empty);
    }
}
