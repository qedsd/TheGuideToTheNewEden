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
    /// <summary>下拉里的一条"常用模型 → 上下文窗口"（阶段 149：按模型自动填入窗口）。</summary>
    private sealed record KnownModel(string Label, int WindowTokens);

    private static readonly KnownModel[] KnownModels =
    [
        new("DeepSeek V3 / R1 / Flash — 1M", 1_000_000),
        new("GPT-4o / 4o-mini — 128k", 128_000),
        new("Claude 3.5 / 3.7 Sonnet — 200k", 200_000),
        new("Claude 4 Sonnet / Opus — 200k", 200_000),
        new("Gemini 1.5 / 2.x — 1M", 1_000_000),
        new("Qwen 2.5 Max / Long — 128k", 128_000),
        new("GLM-4 / Kimi — 128k", 128_000),
    ];

    private bool _fillingKnownModels;

    public AiBehaviorSettingView()
    {
        InitializeComponent();
        // 输入框的**提示**用固定示例（默认值是"不启用"，提示不跟着空——用户要求保留提示）
        HotkeyBox.PlaceholderText = AiAssistantSettingService.HotkeyHint;
        // 只在**构造时**读一次设置：二级菜单之间来回切换时视图是复用的，重新读会丢未保存的编辑
        Load();
        FillKnownModels();
    }

    /// <summary>填充"常用模型"下拉（值都是近似口径，选完仍可手改）。</summary>
    private void FillKnownModels()
    {
        _fillingKnownModels = true;
        try
        {
            KnownModelBox.ItemsSource = KnownModels;
            KnownModelBox.SelectedIndex = -1;
        }
        finally
        {
            _fillingKnownModels = false;
        }
    }

    /// <summary>选了一个"常用模型" → 把它的窗口写进下面的输入框（不自动保存，用户仍可手改）。</summary>
    private void OnKnownModelSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_fillingKnownModels || KnownModelBox.SelectedItem is not KnownModel model)
        {
            return;
        }

        ContextWindowBox.Value = model.WindowTokens;
        KnownModelBox.SelectedIndex = -1;   // 重新放开：下次还能继续选，避免停留在"已经填过"的状态
    }

    /// <summary>把设置读进控件（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        RoundsBox.Value = AiAssistantSettingService.MaxToolRounds;
        CharacterBox.Text = AiAssistantSettingService.DefaultCharacter;
        ConfirmToggle.IsChecked = AiAssistantSettingService.ConfirmExpensive;
        ShowInMainWindowToggle.IsChecked = AiAssistantSettingService.ShowInMainWindow;
        ContextWindowBox.Value = AiAssistantSettingService.ContextWindowTokens;
        SummarizeToggle.IsChecked = AiAssistantSettingService.SummarizeContext;
        OutputReserveBox.Value = AiAssistantSettingService.OutputReserveTokens;
        MaxCallsBox.Value = AiAssistantSettingService.MaxToolCallsPerRound;
        TurnToolTokensBox.Value = AiAssistantSettingService.MaxTurnToolTokens;
        HotkeyBox.Text = AiAssistantSettingService.Hotkey;
        PromptBox.Text = AiAssistantSettingService.SystemPrompt;
    }

    /// <summary>保存本分节的设置（宿主页的"恢复默认"后也会调用 <see cref="Load"/> 回填）。</summary>
    public void Save()
    {
        var rounds = (int)Math.Clamp(RoundsBox.Value ?? AiAssistantSettingService.DefaultMaxToolRounds, 1, 30);
        var contextWindow = (int)Math.Clamp(
            ContextWindowBox.Value ?? AiAssistantSettingService.DefaultContextWindowTokens,
            0,
            AiAssistantSettingService.MaxContextWindowTokensLimit);
        var outputReserve = (int)Math.Clamp(
            OutputReserveBox.Value ?? AiAssistantSettingService.DefaultOutputReserveTokens,
            0,
            AiAssistantSettingService.MaxOutputReserveTokensLimit);
        var maxCalls = (int)Math.Clamp(
            MaxCallsBox.Value ?? AiAssistantSettingService.DefaultMaxToolCallsPerRound,
            1,
            AiAssistantSettingService.MaxToolCallsPerRoundLimit);
        var turnToolTokens = (int)Math.Clamp(
            TurnToolTokensBox.Value ?? AiAssistantSettingService.DefaultMaxTurnToolTokens,
            0,
            AiAssistantSettingService.MaxTurnToolTokensLimit);

        AiAssistantSettingService.Save(
            rounds,
            PromptBox.Text,
            ConfirmToggle.IsChecked == true,
            HotkeyBox.Text,
            CharacterBox.Text,
            contextWindow,
            outputReserve,
            maxCalls,
            turnToolTokens,
            SummarizeToggle.IsChecked == true);
        AiAssistantSettingService.SaveShowInMainWindow(ShowInMainWindowToggle.IsChecked == true);

        // 快捷键改完即时生效（不需要重启）：让主窗口重注册
        App.MainWindow?.RegisterAssistantHotkey();
        PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
