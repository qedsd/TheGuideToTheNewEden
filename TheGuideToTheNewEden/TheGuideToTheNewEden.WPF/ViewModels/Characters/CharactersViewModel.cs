using System.Collections.ObjectModel;
using System.ComponentModel;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>角色页视图模型：角色卡片集合（卡片视图与列表视图共用） + 汇总统计。</summary>
/// <remarks>
/// 集合里**只有真实角色**：末尾的"添加角色"入口从阶段 269 起只在工具栏出现，不再以占位卡/占位行的形式混进集合。
/// </remarks>
public sealed class CharactersViewModel : INotifyPropertyChanged
{
    private bool _isLoading;

    public ObservableCollection<CharacterCardViewModel> Cards { get; } = [];

    public bool IsEmpty => Cards.Count == 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public int CharacterCount => Cards.Count;

    public string TotalSkillPointsText =>
        CharacterCardViewModel.FormatIsk(Cards.Sum(p => (double)p.SkillPoints)).Replace(" ISK", string.Empty);

    public string TotalWalletText =>
        CharacterCardViewModel.FormatIsk(Cards.Sum(p => p.Wallet));

    public long TotalLoyalty => Cards.Sum(p => p.Loyalty);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>重建卡片并加载数据（并行）。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        RaiseSummary();

        var existing = Cards.ToDictionary(p => p.CharacterId);
        Cards.Clear();

        var characters = CharacterStore.Characters.ToList();
        if (characters.Count == 0)
        {
            IsLoading = false;
            RaiseSummary();
            return;
        }

        foreach (var character in characters)
        {
            // 复用已有卡片，避免刷新时头像/数据闪一下
            Cards.Add(existing.TryGetValue(character.CharacterID, out var card)
                ? card
                : new CharacterCardViewModel { Character = character });
        }

        await Task.WhenAll(Cards.Select(p => p.LoadAsync(forceRefresh)));

        IsLoading = false;
        RaiseSummary();
    }

    public void Remove(CharacterCardViewModel card)
    {
        CharacterStore.Remove(card.Character);
        Cards.Remove(card);
        RaiseSummary();
    }

    /// <summary>拖拽卡片后同时调整界面集合与角色存储顺序（CharacterStore.Move 会落盘）。</summary>
    public void MoveCard(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex
            || oldIndex < 0 || newIndex < 0
            || oldIndex >= Cards.Count || newIndex >= Cards.Count
            || oldIndex >= CharacterStore.Characters.Count
            || newIndex >= CharacterStore.Characters.Count)
        {
            return;
        }

        Cards.Move(oldIndex, newIndex);
        CharacterStore.Move(oldIndex, newIndex);
    }

    private void RaiseSummary()
    {
        foreach (var name in new[]
                 {
                     nameof(IsEmpty), nameof(CharacterCount),
                     nameof(TotalSkillPointsText), nameof(TotalWalletText), nameof(TotalLoyalty),
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}