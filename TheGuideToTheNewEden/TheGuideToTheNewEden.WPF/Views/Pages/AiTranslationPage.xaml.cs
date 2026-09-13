using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// AI 翻译页（导航「翻译 → AI 翻译」）：对话式记录界面。
/// VM 挂在页面 DataContext 上，子视图的 <c>Loaded</c>/<c>Unloaded</c> 负责 <c>Init</c>/<c>Dispose</c>。
/// </summary>
public partial class AiTranslationPage : Page
{
    private readonly AiChatTranslationViewModel _viewModel = new();

    public AiTranslationPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }
}
