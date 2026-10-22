using System.ComponentModel;
using TheGuideToTheNewEden.WPF.Services.Industry;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>「参与角色」中的自定义角色条目。</summary>
public sealed class IndustryCustomCharacterItem : INotifyPropertyChanged
{
    public IndustryCustomCharacterItem(IndustryCustomCharacter model, bool selected)
    {
        Model = model;
        var profile = IndustryCharacterProfileFactory.CreateCustom(model);
        Summary = $"制造 {profile.ManufacturingSlots} · 发明 {profile.InventionSlots} · 反应 {profile.ReactionSlots}"
            + $" · 制造×{profile.ManufacturingTimeMultiplier:0.00} · 反应×{profile.ReactionTimeMultiplier:0.00}";
        _isSelected = selected;
    }

    public IndustryCustomCharacter Model { get; }
    public long Id => Model.Id;
    public string Name => string.IsNullOrWhiteSpace(Model.Name) ? "自定义角色" : Model.Name;
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
