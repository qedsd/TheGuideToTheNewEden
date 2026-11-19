using System.Windows.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Controls;

/// <summary>
/// 「战斗异常」页详情 Tab 的内容容器：显示站点名与 Markdown 正文。
/// DataContext 为 <c>CombatSite</c>。
/// </summary>
public partial class CombatSiteDetailControl : UserControl
{
    public CombatSiteDetailControl()
    {
        InitializeComponent();
    }
}
