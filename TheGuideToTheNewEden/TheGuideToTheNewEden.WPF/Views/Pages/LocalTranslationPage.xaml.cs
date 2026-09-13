using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 本地词库翻译页（导航「翻译 → 本地词库」）：左匹配列表 + 右译文详情，输入即查、离线不计费。
/// VM 挂在页面 DataContext 上，子视图的 <c>Loaded</c>/<c>Unloaded</c> 负责 <c>Init</c>/<c>Dispose</c>。
/// </summary>
public partial class LocalTranslationPage : Page
{
    private readonly LocalTranslationViewModel _viewModel = new();

    public LocalTranslationPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }
}
