using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// KB 流设置视图（由 <c>KillStreamPage</c> 以 ToolWindow 弹窗承载）：
/// 通用开关/阈值 + 三组过滤黑白名单，直接绑定宿主页的 <see cref="KillStreamViewModel"/>（同一份 Config，改动即生效并随流服务保存）。
/// </summary>
public partial class KillStreamSettingView : UserControl
{
    public KillStreamSettingView(KillStreamViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
