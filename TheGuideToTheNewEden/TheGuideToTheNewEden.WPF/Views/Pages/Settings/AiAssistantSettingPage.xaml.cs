using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 「设置 → AI 助手」：助手的行为参数（最大工具轮数 / 默认角色 / 写操作确认 / 全局快捷键 / 自定义提示词）。
/// **模型端点不在这里**——助手复用「设置 → AI 翻译」的端点（协议 / 地址 / Key / 模型）。
/// </summary>
public partial class AiAssistantSettingPage : Page
{
    public AiAssistantSettingPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        RoundsBox.Value = AiAssistantSettingService.MaxToolRounds;
        CharacterBox.Text = AiAssistantSettingService.DefaultCharacter;
        ConfirmToggle.IsChecked = AiAssistantSettingService.ConfirmExpensive;
        HotkeyBox.Text = AiAssistantSettingService.Hotkey;
        PromptBox.Text = AiAssistantSettingService.SystemPrompt;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var rounds = (int)Math.Clamp(RoundsBox.Value ?? AiAssistantSettingService.DefaultMaxToolRounds, 1, 30);
        AiAssistantSettingService.Save(
            rounds,
            PromptBox.Text,
            ConfirmToggle.IsChecked == true,
            HotkeyBox.Text,
            CharacterBox.Text);

        // 快捷键改完即时生效（不需要重启）：让主窗口重注册
        App.MainWindow?.RegisterAssistantHotkey();

        PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        AiAssistantSettingService.RestoreDefaults();
        Load();
        App.MainWindow?.RegisterAssistantHotkey();
        PageNotifyService.Info(FindString("AssistantSettingPage_Restored"));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
