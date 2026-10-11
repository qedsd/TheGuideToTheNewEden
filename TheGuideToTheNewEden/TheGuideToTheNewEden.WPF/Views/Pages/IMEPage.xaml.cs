using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.ViewModels;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 输入助手页：实时给出游戏名称建议（点建议替换当前词），并提供 复制 / 搜索 / 翻译。
/// 对应 WinUI 版 <c>Views/Tools/IMEPage</c>（那里也是独立工具窗，原尺寸 500×200），
/// 逻辑见 <see cref="IMEPageViewModel"/>。
/// <para>页面在**独立工具窗口**里打开（见 <see cref="IToolPage"/>），不占主窗口内容区。</para>
/// </summary>
public partial class IMEPage : Page, IToolPage
{
    private readonly IMEPageViewModel _viewModel = new();

    public IMEPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>默认尺寸（同时是窗口的最小尺寸，见 ToolWindowHost）：400×200。</summary>
    public int PreferredWidth => 400;

    public int PreferredHeight => 200;

    /// <summary>没有后台工作，关闭即结束。</summary>
    public void OnToolClosed()
    {
    }

    private void OnSuggestionClick(object sender, RoutedEventArgs e)
        => _viewModel.ApplySuggestion((sender as FrameworkElement)?.DataContext as IdName);

    private void OnCopyClick(object sender, RoutedEventArgs e)
        => _viewModel.Copy();

    private async void OnSearchClick(object sender, RoutedEventArgs e)
        => await _viewModel.SearchAsync(GetSelectedText());

    private async void OnTranslateClick(object sender, RoutedEventArgs e)
        => await _viewModel.TranslateAsync(GetSelectedText());

    private void OnBackClick(object sender, RoutedEventArgs e)
        => _viewModel.CloseDetail();

    /// <summary>有选中内容就只处理选中部分（与 WinUI 一致）。</summary>
    private string? GetSelectedText()
        => InputBox.SelectionLength > 0
            ? InputBox.Text.Substring(InputBox.SelectionStart, InputBox.SelectionLength)
            : null;
}
