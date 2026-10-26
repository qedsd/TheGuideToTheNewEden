using System.ComponentModel;
using TheGuideToTheNewEden.WPF.Services.Industry;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>「参与角色」中的自定义角色条目。</summary>
public sealed class IndustryCustomCharacterItem : INotifyPropertyChanged
{
    public IndustryCustomCharacterItem(IndustryCustomCharacter model, bool selected)
    {
        Model = model;
        var profile = IndustryCharacterProfileFactory.CreateCustom(model);
        Summary = LanguageService.Format("IndustryCustomCharacter_Summary",
            profile.ManufacturingSlots, profile.InventionSlots, profile.ReactionSlots,
            profile.ManufacturingTimeMultiplier, profile.ReactionTimeMultiplier);
        _isSelected = selected;
    }

    public IndustryCustomCharacter Model { get; }
    public long Id => Model.Id;
    public string Name => string.IsNullOrWhiteSpace(Model.Name)
        ? LanguageService.Get("IndustryCustomCharacter_DefaultName")
        : Model.Name;
    public string Summary { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
