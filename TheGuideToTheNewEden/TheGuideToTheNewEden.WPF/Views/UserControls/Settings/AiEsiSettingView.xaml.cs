using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Settings;

/// <summary>
/// 设置 → AI → 「ESI 全量访问」：网关开关 / 允许写 / ID 富化 / 页数上限 / 结果字符上限 / 错误配额阈值 / 黑名单。
/// <para>阶段 124 从 <c>AiAssistantSettingPage</c> 拆出来，作为二级菜单的一个分节；本视图自己带「保存」。</para>
/// </summary>
public partial class AiEsiSettingView : UserControl
{
    public AiEsiSettingView()
    {
        InitializeComponent();
        // 只在**构造时**读一次设置（切来切去不丢未保存的编辑）
        Load();
    }

    /// <summary>把设置读进控件（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        EsiToggle.IsChecked = AiAssistantSettingService.EsiEnabled;
        EsiWriteToggle.IsChecked = AiAssistantSettingService.EsiAllowWrites;
        EsiEnrichToggle.IsChecked = AiAssistantSettingService.EsiEnrichNames;
        EsiPagesBox.Value = AiAssistantSettingService.EsiMaxPages;
        EsiCharsBox.Value = AiAssistantSettingService.EsiMaxResultChars;
        EsiErrorBox.Value = AiAssistantSettingService.EsiErrorLimitFloor;
        EsiDenyBox.Text = AiAssistantSettingService.EsiDeniedPaths;
    }

    /// <summary>保存本分节的设置。</summary>
    public void Save()
    {
        AiAssistantSettingService.SaveEsi(
            EsiToggle.IsChecked == true,
            EsiWriteToggle.IsChecked == true,
            EsiEnrichToggle.IsChecked == true,
            (int)Math.Clamp(EsiPagesBox.Value ?? AiAssistantSettingService.DefaultEsiMaxPages, 1, 10),
            (int)Math.Clamp(EsiCharsBox.Value ?? AiAssistantSettingService.DefaultEsiMaxResultChars, 500, 12000),
            (int)Math.Clamp(EsiErrorBox.Value ?? AiAssistantSettingService.DefaultEsiErrorLimitFloor, 0, 100),
            EsiDenyBox.Text);

        PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
