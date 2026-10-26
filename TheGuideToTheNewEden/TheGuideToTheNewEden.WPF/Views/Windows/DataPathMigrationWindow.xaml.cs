using System.Windows;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 首次运行（4.0+）检测到旧版本配置时的数据目录选择框（阶段 186）。
/// 两个选项：使用按大版本隔离的新目录（可把旧配置复制过来） / 沿用旧目录（与旧版本共用，有冲突风险）。
/// 直接点窗口关闭键按"新目录 + 复制旧配置"处理，避免用户被卡在启动前。
/// </summary>
public partial class DataPathMigrationWindow : Wpf.Ui.Controls.FluentWindow
{
    private bool _confirmed;

    public DataPathMigrationWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => FillTexts();
    }

    /// <summary>true = 沿用旧目录（与旧版本共用）。</summary>
    public bool UseLegacyFolder { get; private set; }

    /// <summary>是否把旧版本的配置与登录信息复制到新目录。</summary>
    public bool CopyLegacyConfig { get; private set; } = true;

    private void FillTexts()
    {
        var legacy = DataPathService.LegacyDataPath;
        var target = DataPathService.DefaultDataPath;

        IntroText.Text = LanguageService.Format("DataPathMigration_Intro", legacy);
        LegacyPathText.Text = legacy;
        WarningText.Text = LanguageService.Get("DataPathMigration_Warning");
        NewPathText.Text = target;
        NewFolderDescription.Text = LanguageService.Format("DataPathMigration_UseNewDesc", target);
        LegacyFolderDescription.Text = LanguageService.Format("DataPathMigration_UseLegacyDesc", legacy);
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        UseLegacyFolder = UseLegacyFolderRadio.IsChecked == true;
        CopyLegacyConfig = CopyLegacyCheckBox.IsChecked == true;
        _confirmed = true;
        // 设置 DialogResult 会自动关闭 ShowDialog 打开的窗口
        DialogResult = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 没点"继续"就关窗：按推荐方案（新目录 + 复制旧配置）继续启动
        if (!_confirmed)
        {
            UseLegacyFolder = false;
            CopyLegacyConfig = true;
        }
        base.OnClosing(e);
    }
}
