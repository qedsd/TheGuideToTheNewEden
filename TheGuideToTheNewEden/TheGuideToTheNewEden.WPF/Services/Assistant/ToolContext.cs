using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>工具执行上下文：当前角色、UI 线程归位、进度回调。</summary>
public sealed class ToolContext
{
    /// <summary>当前角色（默认角色或按名匹配；角色类工具用）。</summary>
    public required AuthorizedCharacterData? Character { get; init; }

    /// <summary>全部已登录角色（character_list 等用）。</summary>
    public required IReadOnlyList<AuthorizedCharacterData> Characters { get; init; }

    /// <summary>把动作归位到 UI 线程（导航等必须）。</summary>
    public required Action<Action> OnUiThread { get; init; }

    /// <summary>进度回调（重型工具：倒货/比价取数等，写入工具步骤区）。</summary>
    public Action<string>? Progress { get; init; }
}
