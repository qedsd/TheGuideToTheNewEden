using System.Windows;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 通用单行文本输入对话框（配置文件的新建 / 重命名 / 复制命名共用）。
/// <para>
/// 校验放在调用方传入的 <paramref name="validate"/> 里：返回非空字符串即视为错误，
/// 就地显示错误文案、<b>不关窗</b>（这样"重名 / 名称非法"不必关掉再重开一次）。
/// </para>
/// </summary>
public partial class EveSettingsTextInputWindow : FluentWindow
{
    private readonly Func<string, string?>? _validate;

    public EveSettingsTextInputWindow(
        string title,
        string label,
        string initialValue,
        string? placeholder = null,
        Func<string, string?>? validate = null)
    {
        InitializeComponent();

        TitleBar.Title = title;
        Title = title;
        LabelText.Text = label;
        InputBox.PlaceholderText = placeholder ?? string.Empty;
        InputBox.Text = initialValue;
        _validate = validate;

        InputBox.TextChanged += (_, _) => ErrorText.Text = string.Empty;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    /// <summary>用户确认的文本（已 Trim）。仅当 <c>DialogResult == true</c> 时有意义。</summary>
    public string Value { get; private set; } = string.Empty;

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var value = InputBox.Text?.Trim() ?? string.Empty;
        var error = _validate?.Invoke(value);
        if (!string.IsNullOrEmpty(error))
        {
            ErrorText.Text = error;
            return;
        }

        Value = value;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
