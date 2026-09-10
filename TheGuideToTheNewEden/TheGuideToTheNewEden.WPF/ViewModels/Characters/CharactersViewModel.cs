using System.Collections.ObjectModel;
using System.ComponentModel;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>角色卡片页视图模型：卡片集合 + 汇总统计。</summary>
public sealed class CharactersViewModel : INotifyPropertyChanged
{
    private bool _isLoading;

    public ObservableCollection<CharacterCardViewModel> Cards { get; } = [];

    public bool IsEmpty => Cards.Count(p => !p.IsAdd) == 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public int CharacterCount => Cards.Count(p => !p.IsAdd);

    public string TotalSkillPointsText =>
        CharacterCardViewModel.FormatIsk(Cards.Where(p => !p.IsAdd).Sum(p => (double)p.SkillPoints)).Replace(" ISK", string.Empty);

    public string TotalWalletText =>
        CharacterCardViewModel.FormatIsk(Cards.Where(p => !p.IsAdd).Sum(p => p.Wallet));

    public long TotalLoyalty => Cards.Where(p => !p.IsAdd).Sum(p => p.Loyalty);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>重建卡片并加载数据（并行）。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        RaiseSummary();

        var existing = Cards.Where(p => !p.IsAdd).ToDictionary(p => p.CharacterId);
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

        Cards.Add(new CharacterCardViewModel { Character = new AuthorizedCharacterData(), IsAdd = true });

        await Task.WhenAll(Cards.Where(p => !p.IsAdd).Select(p => p.LoadAsync(forceRefresh)));

        IsLoading = false;
        RaiseSummary();
    }

    public void Remove(CharacterCardViewModel card)
    {
        if (card.IsAdd)
        {
            return;
        }

        CharacterStore.Remove(card.Character);
        Cards.Remove(card);
        RaiseSummary();
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