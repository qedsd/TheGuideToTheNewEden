using System.Windows.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 翻译页：EVE 专有名词的中英互译（物品 / 星域 / 星系 / 空间站）。
/// 数据来自本地 SDE 数据库（主库英文 + 本地化库中文），**完全离线**——
/// 旧版 WinUI 的有道 API 方案已废弃（见 REFACTORING.md 阶段 46）。
/// 界面与逻辑都在 <see cref="UserControls.TranslationPanelView"/>（与"弹窗"共用）。
/// </summary>
public partial class TranslationPage : Page
{
    public TranslationPage()
    {
        InitializeComponent();
    }
}
