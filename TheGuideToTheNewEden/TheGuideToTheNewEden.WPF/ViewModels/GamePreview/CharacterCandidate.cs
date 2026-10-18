namespace TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

/// <summary>
/// 分组快捷键的"候选角色"：当前发现的客户端角色、已保存配置里的角色、以及其他分组里手工加过的名字。
/// <para>
/// 只是给点选胶囊用的只读快照——列表在 <see cref="GamePreviewViewModel"/> 里按"角色名 + 是否预览中"
/// 重建（内容不变就不重建），因此不需要 <c>INotifyPropertyChanged</c>。
/// </para>
/// </summary>
/// <param name="Name">角色名（与 <c>ProcessInfo.GetCharacterName()</c> / <c>PreviewItem.Name</c> 同口径）。</param>
/// <param name="IsPreviewing">该角色此刻是否正在预览（胶囊上的绿点）。</param>
public sealed record CharacterCandidate(string Name, bool IsPreviewing);
