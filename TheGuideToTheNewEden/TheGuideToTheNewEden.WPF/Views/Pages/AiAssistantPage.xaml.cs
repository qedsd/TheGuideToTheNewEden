using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.ViewModels.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// AI 助手页（导航「AI 助手」）：对话式记录界面，自然语言调用软件功能。
/// VM 挂在页面 DataContext 上，实例来自 <see cref="AssistantHost"/>（与弹窗、全局快捷键**共享同一个 VM**）；
/// <c>Init</c> / <c>Dispose</c> 由本页与弹窗按需调用（两者都调用是安全的）。
/// </summary>
public partial class AiAssistantPage : Page
{
    /// <summary>共享的助手 VM（弹窗与快捷键用同一实例）。</summary>
    internal AiAssistantViewModel ViewModel => AssistantHost.ViewModel;

    public AiAssistantPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += (_, _) => ViewModel.Init();
        Unloaded += (_, _) => ViewModel.Dispose();
    }
}
