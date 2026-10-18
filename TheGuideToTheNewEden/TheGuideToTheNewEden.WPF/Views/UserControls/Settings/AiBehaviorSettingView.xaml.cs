using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Settings;

/// <summary>
/// 设置 → AI → 「行为」：最大工具轮数 / 默认角色 / 写操作确认 / 全局快捷键 / 助手系统提示词。
/// <para>阶段 124 从 <c>AiAssistantSettingPage</c> 拆出来，作为二级菜单的一个分节；本视图自己带「保存」。</para>
/// </summary>
public partial class AiBehaviorSettingView : UserControl
{
    public AiBehaviorSettingView()
    {
        InitializeComponent();
        // 输入框的**提示**用固定示例（默认值是"不启用"，提示不跟着空——用户要求保留提示）
        HotkeyBox.PlaceholderText = AiAssistantSettingService.HotkeyHint;
        // 只在**构造时**读一次设置：二级菜单之间来回切换时视图是复用的，重新读会丢未保存的编辑
        Load();
    }

    /// <summary>把设置读进控件（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        RoundsBox.Value = AiAssistantSettingService.MaxToolRounds;
        CharacterBox.Text = AiAssistantSettingService.DefaultCharacter;
        ConfirmToggle.IsChecked = AiAssistantSettingService.ConfirmExpensive;
        ShowInMainWindowToggle.IsChecked = AiAssistantSettingService.ShowInMainWindow;
        ContextCharsBox.Value = AiAssistantSettingService.MaxContextChars;
        HotkeyBox.Text = AiAssistantSettingService.Hotkey;
        PromptBox.Text = AiAssistantSettingService.SystemPrompt;
    }

    /// <summary>保存本分节的设置（宿主页的"恢复默认"后也会调用 <see cref="Load"/> 回填）。</summary>
    public void Save()
    {
        var rounds = (int)Math.Clamp(RoundsBox.Value ?? AiAssistantSettingService.DefaultMaxToolRounds, 1, 30);
        var contextChars = (int)Math.Clamp(
            ContextCharsBox.Value ?? AiAssistantSettingService.DefaultMaxContextChars,
            0,
            AiAssistantSettingService.MaxContextCharsLimit);
        AiAssistantSettingService.Save(
            rounds,
            PromptBox.Text,
            ConfirmToggle.IsChecked == true,
            HotkeyBox.Text,
            CharacterBox.Text,
            contextChars);
        AiAssistantSettingService.SaveShowInMainWindow(ShowInMainWindowToggle.IsChecked == true);

        // 快捷键改完即时生效（不需要重启）：让主窗口重注册
        App.MainWindow?.RegisterAssistantHotkey();
        PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
