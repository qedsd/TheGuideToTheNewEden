using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Models.Planetary;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Planetary;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Industry;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed class PlanetaryProductItem
{
    public PlanetaryProductItem(PlanetaryProduct product)
    {
        Product = product;
        Name = PlanetaryNameLocalizer.TypeName(product.TypeId, product.Name);
    }

    public PlanetaryProduct Product { get; }
    public int TypeId => Product.TypeId;
    public string Name { get; }
    public string TierText => "P" + (int)Product.Tier;
    public string DisplayName => $"{TierText} · {Name}";
}

public sealed class PlanetaryBlueprintItem
{
    public PlanetaryBlueprintItem(
        IndustryProductEntry entry,
        string productName,
        string blueprintName)
    {
        Entry = entry;
        ProductName = productName;
        BlueprintName = blueprintName;
        SearchText = productName + "\n" + entry.ProductName + "\n" + blueprintName + "\n" + entry.BlueprintName;
    }

    public IndustryProductEntry Entry { get; }
    public string ProductName { get; }
    public string BlueprintName { get; }
    public string SearchText { get; }
}

public sealed class PlanetaryCharacterItem : INotifyPropertyChanged
{
    public PlanetaryCharacterItem(AuthorizedCharacterData character, bool selected)
    {
        Character = character;
        _isSelected = selected;
    }

    public AuthorizedCharacterData Character { get; }
    public long CharacterId => Character.CharacterID;
    public string DisplayName => Character.CharacterName;
    public string Summary => LanguageService.Format("IndustryPlanetary_Summary", CcuLevel, MaxPlanets);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Notify();
        }
    }

    private int _ccuLevel = 5;
    public int CcuLevel
    {
        get => _ccuLevel;
        set
        {
            value = Math.Max(0, Math.Min(5, value));
            if (_ccuLevel == value) return;
            _ccuLevel = value;
            Notify();
            Notify(nameof(Summary));
        }
    }

    private int _maxPlanets = 6;
    public int MaxPlanets
    {
        get => _maxPlanets;
        set
        {
            value = Math.Max(1, Math.Min(6, value));
            if (_maxPlanets == value) return;
            _maxPlanets = value;
            Notify();
            Notify(nameof(Summary));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>左栏"已选星系"的一项：由候选行星按星系聚合而来（行星被删空时整项消失）。</summary>
public sealed class PlanetarySelectedSystemItem
{
    public int SolarSystemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<PlanetaryPlanetOption> Planets { get; init; } = Array.Empty<PlanetaryPlanetOption>();
    public int PlanetCount => Planets.Count;
    public string CountText => LanguageService.Format("IndustryPlanetary_PlanetsCount", PlanetCount);
    public override string ToString() => Name;
}

public sealed partial class PlanetaryPlanPageViewModel : INotifyPropertyChanged
{
    private PlanetaryRecipeCatalog? _recipes;
    private PlanetTypeCatalog? _planetTypes;
    private PlanetaryPlanner? _planner;
    private PlanetaryPlanSetting _setting;
    private List<PlanetaryBlueprintItem> _blueprintItems = new();
    private readonly Dictionary<int, double> _prices = new();
    private readonly Dictionary<int, double> _liveSupplyPerDay = new();
    private CancellationTokenSource? _operationCts;
    private bool _loaded;
    private bool _suspendSettingSave;
    private bool _saveTimerHooked;
    private System.Windows.Threading.DispatcherTimer? _settingSaveTimer;

    public PlanetaryPlanPageViewModel()
    {
        _setting = PlanetaryPlanSettingService.Load() ?? new PlanetaryPlanSetting();
        _suspendSettingSave = true;
        try
        {
            _selectedPriceLocation = _setting.PriceLocation;
            _planetTaxPercent = _setting.PlanetTaxPercent;
            _salesTaxPercent = _setting.SalesTaxPercent;
            _brokerFeePercent = _setting.BrokerFeePercent;
            _ccuLevel = Math.Clamp(_setting.CcuLevel, 0, 5);
            _maxPlanets = Math.Clamp(_setting.MaxPlanets, 1, 6);
            _radiusKm = Math.Max(0, Math.Round(_setting.RadiusKm));
            _cycleDays = Math.Max(0.1, _setting.CycleDays);
            _periodDays = Math.Max(1, Math.Round(_setting.PeriodDays));
            _modeIndex = Math.Clamp(_setting.ModeIndex, 0, 2);
            _useSetupCost = _setting.UseSetupCost;
            _useCurrentSupply = _setting.UseCurrentSupply;
            _useMarketPrices = _setting.UseMarketPrices;

            var savedCharacters = (_setting.Characters ?? new List<PlanetaryCharacterSetting>())
                .GroupBy(p => p.CharacterId)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var character in CharacterStore.Characters)
            {
                var hasSaved = savedCharacters.TryGetValue(character.CharacterID, out var saved);
                var item = new PlanetaryCharacterItem(character, !hasSaved || saved!.Selected);
                if (hasSaved && saved is not null)
                {
                    item.CcuLevel = saved.CcuLevel;
                    item.MaxPlanets = saved.MaxPlanets;
                }
                item.PropertyChanged += (_, _) => ScheduleSettingSave();
                Characters.Add(item);
            }
        }
        finally
        {
            _suspendSettingSave = false;
        }

        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// 语言切换：重算"生成时拼进对象"的文案（计划提示、已保存计划摘要、行星类型名），
    /// 其余计算属性由 <see cref="LanguageService"/> 的绑定重算负责。
    /// </summary>
    private void OnLanguageChanged(object? sender, string language)
    {
        if (CurrentPlan is not null)
        {
            LocalizePlan(CurrentPlan);
            RefreshPlanWarnings();
        }

        ReloadSavedPlans();
        RefreshLiveCharacterSummary();
        if (!IsLiveBusy)
        {
            LiveStatusText = LanguageService.Get("IndustryPlanetary_Live_Reading");
        }
        Notify(nameof(MarketStatusText));
        Notify(nameof(PlanetSourceText));
        Notify(nameof(PlanetSummaryText));
        Notify(nameof(PlanetListHeaderText));
        Notify(nameof(RadiusHintText));
        Notify(nameof(TargetsCountText));
        Notify(nameof(TargetDaysText));
        Notify(nameof(PaybackDaysText));
        Notify(nameof(SelectedCharacterSummaryText));
        Notify(nameof(SelectedBlueprintText));
        Notify(nameof(PriceLocationText));
        Notify(nameof(SavedPlanCountText));
        RebuildProductTree();
    }

    /// <summary>立即把当前计划设置写盘（防抖计时器与"执行计划"都会调用）。</summary>
    public void SaveSettings()
    {
        _settingSaveTimer?.Stop();
        if (_suspendSettingSave)
        {
            return;
        }

        _setting.PeriodDays = PeriodDays;
        _setting.ModeIndex = ModeIndex;
        _setting.CcuLevel = (int)CcuLevel;
        _setting.MaxPlanets = (int)MaxPlanets;
        _setting.RadiusKm = RadiusKm;
        _setting.CycleDays = CycleDays;
        _setting.PlanetTaxPercent = PlanetTaxPercent;
        _setting.SalesTaxPercent = SalesTaxPercent;
        _setting.BrokerFeePercent = BrokerFeePercent;
        _setting.UseSetupCost = UseSetupCost;
        _setting.UseCurrentSupply = UseCurrentSupply;
        _setting.UseMarketPrices = UseMarketPrices;
        _setting.PriceLocation = SelectedPriceLocation;
        _setting.Characters = Characters.Select(p => new PlanetaryCharacterSetting
        {
            CharacterId = p.CharacterId,
            Selected = p.IsSelected,
            CcuLevel = p.CcuLevel,
            MaxPlanets = p.MaxPlanets,
        }).ToList();
        PlanetaryPlanSettingService.Save(_setting);
    }

    /// <summary>参数变化后防抖保存（400ms），避免连续拖动数字框时反复写盘。</summary>
    private void ScheduleSettingSave()
    {
        if (_suspendSettingSave)
        {
            return;
        }

        _settingSaveTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        if (!_saveTimerHooked)
        {
            _saveTimerHooked = true;
            _settingSaveTimer.Tick += (_, _) =>
            {
                _settingSaveTimer?.Stop();
                SaveSettings();
            };
        }
        _settingSaveTimer.Stop();
        _settingSaveTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PlanetaryProductItem> Products { get; } = new();
    public ObservableCollection<PlanetaryProductItem> FilteredProducts { get; } = new();
    public ObservableCollection<IndustryTargetItem> Targets { get; } = new();
    public ObservableCollection<PlanetaryBlueprintItem> BlueprintResults { get; } = new();
    public ObservableCollection<PlanetaryCharacterItem> Characters { get; } = new();
    public ObservableCollection<PlanetarySavedPlan> SavedPlans { get; } = new();

    private int _sectionTabIndex;
    public int SectionTabIndex
    {
        get => _sectionTabIndex;
        set
        {
            if (!Set(ref _sectionTabIndex, value)) return;
            Notify(nameof(ShowPlanSaveButton));
        }
    }

    /// <summary>「计划详细」页签右上角的保存按钮：只有在该页且有结果时才显示。</summary>
    public bool ShowPlanSaveButton => SectionTabIndex == 2 && HasResult;

    private int _settingsTabIndex;
    public int SettingsTabIndex { get => _settingsTabIndex; set => Set(ref _settingsTabIndex, value); }

    private string _productSearchText = string.Empty;
    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (!Set(ref _productSearchText, value)) return;
            RefreshFilteredProducts();
        }
    }

    private PlanetaryProductItem? _selectedProduct;
    public PlanetaryProductItem? SelectedProduct
    {
        get => _selectedProduct;
        set => Set(ref _selectedProduct, value);
    }

    private double _targetQuantity = 100;
    public double TargetQuantity
    {
        get => _targetQuantity;
        set => Set(ref _targetQuantity, Math.Max(1, Math.Round(value)));
    }

    private double _periodDays = 7;
    public double PeriodDays
    {
        get => _periodDays;
        set
        {
            if (!Set(ref _periodDays, Math.Max(1, Math.Round(value)))) return;
            ScheduleSettingSave();
        }
    }

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            if (!Set(ref _modeIndex, Math.Max(0, Math.Min(2, value)))) return;
            ScheduleSettingSave();
        }
    }

    private double _ccuLevel = 5;
    public double CcuLevel
    {
        get => _ccuLevel;
        set
        {
            if (!Set(ref _ccuLevel, Math.Max(0, Math.Min(5, Math.Round(value))))) return;
            ScheduleSettingSave();
        }
    }

    private double _maxPlanets = 6;
    public double MaxPlanets
    {
        get => _maxPlanets;
        set
        {
            if (!Set(ref _maxPlanets, Math.Max(1, Math.Min(6, Math.Round(value))))) return;
            ScheduleSettingSave();
        }
    }

    private double _radiusKm;
    public double RadiusKm
    {
        get => _radiusKm;
        set
        {
            if (!Set(ref _radiusKm, Math.Max(0, Math.Round(value)))) return;
            Notify(nameof(RadiusHintText));
            Notify(nameof(PlanetSourceText));
            ScheduleSettingSave();
        }
    }

    // ---- 候选行星（多星系、多行星）：规划器只把殖民地排到这些行星上 ----

    public ObservableCollection<PlanetaryPlanetOption> SelectedPlanets { get; } = new();

    /// <summary>由候选行星派生的星系列表（左栏）；某星系的行星被全部移除时它会自动消失。</summary>
    public ObservableCollection<PlanetarySelectedSystemItem> SelectedSystems { get; } = new();

    /// <summary>右栏当前显示的行星（选中的星系；未选中时显示全部）。</summary>
    public ObservableCollection<PlanetaryPlanetOption> VisiblePlanets { get; } = new();

    private PlanetarySelectedSystemItem? _selectedSystemItem;
    public PlanetarySelectedSystemItem? SelectedSystemItem
    {
        get => _selectedSystemItem;
        set
        {
            if (!Set(ref _selectedSystemItem, value)) return;
            RefreshVisiblePlanets();
        }
    }

    public bool HasSelectedPlanets => SelectedPlanets.Count > 0;
    public bool HasNoSelectedPlanets => SelectedPlanets.Count == 0;
    public bool HasSelectedSystems => SelectedSystems.Count > 0;
    public bool HasNoSelectedSystems => SelectedSystems.Count == 0;
    public bool HasVisiblePlanets => VisiblePlanets.Count > 0;
    public bool HasNoVisiblePlanets => VisiblePlanets.Count == 0;

    /// <summary>选了候选行星后，手动半径不再生效（每颗行星用自己的真实半径）。</summary>
    public bool IsRadiusEditable => !HasSelectedPlanets;

    public string PlanetSummaryText => SelectedPlanets.Count == 0
        ? LanguageService.Get("IndustryPlanetary_NoPlanetsSelected")
        : LanguageService.Format("IndustryPlanetary_PlanetsSystems", SelectedPlanets.Count, SelectedSystems.Count);

    public string SystemsHeaderText => LanguageService.Format("IndustryPlanetary_SystemsHeader", SelectedSystems.Count);

    public string PlanetListHeaderText => SelectedSystemItem is null
        ? LanguageService.Format("IndustryPlanetary_PlanetsHeader", SelectedPlanets.Count)
        : $"{SelectedSystemItem.Name}（{SelectedSystemItem.PlanetCount}）";

    public string RadiusHintText => HasSelectedPlanets
        ? LanguageService.Get("IndustryPlanetary_RadiusFromPlanets")
        : RadiusKm > 0
            ? LanguageService.Format("IndustryPlanetary_RadiusManual", RadiusKm)
            : LanguageService.Format("IndustryPlanetary_RadiusEstimated",
                PlanetaryCapacityCalculator.DefaultRadiusKm);

    /// <summary>概况里的短状态。</summary>
    public string PlanetSourceText => CurrentPlan is not null && CurrentPlan.Summary.PlanetCount > 0
        ? LanguageService.Format("IndustryPlanetary_PlanetPoolUsed",
            CurrentPlan.Summary.PlanetCount, CurrentPlan.Summary.UsedPlanetCount)
        : HasSelectedPlanets
            ? LanguageService.Format("IndustryPlanetary_CandidatePlanets", SelectedPlanets.Count)
            : RadiusKm > 0
                ? LanguageService.Format("IndustryPlanetary_NoCandidateManual", RadiusKm)
                : LanguageService.Get("IndustryPlanetary_NoCandidateEstimate");

    public string PlanetSourceTooltip => LanguageService.Get(HasSelectedPlanets
        ? "IndustryPlanetary_CandidateHint"
        : "IndustryPlanetary_NoCandidateHint");

    /// <summary>把某个星系的行星全部加入候选池（去重），返回新增数量。</summary>
    public int AddSystemPlanets(PlanetarySystemOption? system)
    {
        if (system is null || system.SolarSystemId <= 0)
        {
            return 0;
        }

        var planets = PlanetaryPlanetCatalog.GetPlanets(system)
            .Where(p => !p.IsNone && p.PlanetId > 0)
            .ToList();
        if (planets.Count == 0)
        {
            PageNotifyService.Info(LanguageService.Get("IndustryPlanetary_NoHabitPlanets"));
            return 0;
        }

        var added = AddPlanets(planets);
        SelectedSystemItem = SelectedSystems.FirstOrDefault(p => p.SolarSystemId == system.SolarSystemId);
        return added;
    }

    /// <summary>加入行星（按 PlanetId 去重），返回新增数量。</summary>
    public int AddPlanets(IEnumerable<PlanetaryPlanetOption> planets)
    {
        var existing = SelectedPlanets.Select(p => p.PlanetId).ToHashSet();
        var added = 0;
        foreach (var planet in planets)
        {
            if (planet is null || planet.IsNone || planet.PlanetId <= 0 || !existing.Add(planet.PlanetId))
            {
                continue;
            }
            SelectedPlanets.Add(planet);
            added++;
        }

        if (added > 0)
        {
            RefreshPlanetState();
        }
        return added;
    }

    public void RemovePlanet(PlanetaryPlanetOption? planet)
    {
        if (planet is null || !SelectedPlanets.Remove(planet))
        {
            return;
        }
        RefreshPlanetState();
    }

    public void ClearSelectedPlanets()
    {
        if (SelectedPlanets.Count == 0)
        {
            return;
        }
        SelectedPlanets.Clear();
        RefreshPlanetState();
    }

    /// <summary>载入已保存计划时还原候选行星（本地库查不到的用保存值兜底）。</summary>
    public void LoadSavedPlanets(IEnumerable<PlanetaryPlanetInput>? planets)
    {
        SelectedPlanets.Clear();
        foreach (var input in planets ?? Enumerable.Empty<PlanetaryPlanetInput>())
        {
            if (input is null || input.PlanetId <= 0)
            {
                continue;
            }

            var option = PlanetaryPlanetCatalog.GetPlanet(input.PlanetId);
            if (option is null)
            {
                option = new PlanetaryPlanetOption
                {
                    PlanetId = input.PlanetId,
                    Name = string.IsNullOrWhiteSpace(input.Name) ? ("Planet " + input.PlanetId) : input.Name,
                    TypeId = input.TypeId,
                    PlanetType = PlanetaryPlanetCatalog.ResolvePlanetType(input.TypeName),
                    RadiusKm = input.RadiusKm,
                    SolarSystemId = input.SolarSystemId,
                    SolarSystemName = input.SolarSystemName,
                };
            }

            if (SelectedPlanets.All(p => p.PlanetId != option.PlanetId))
            {
                SelectedPlanets.Add(option);
            }
        }
        RefreshPlanetState();
    }

    private void RefreshPlanetState()
    {
        RebuildSelectedSystems();
        Notify(nameof(HasSelectedPlanets));
        Notify(nameof(HasNoSelectedPlanets));
        Notify(nameof(IsRadiusEditable));
        Notify(nameof(PlanetSummaryText));
        Notify(nameof(RadiusHintText));
        Notify(nameof(PlanetSourceText));
        Notify(nameof(PlanetSourceTooltip));
    }

    /// <summary>按候选行星重建左栏星系；右栏保持/回退到某个星系。</summary>
    private void RebuildSelectedSystems()
    {
        var selectedId = _selectedSystemItem?.SolarSystemId ?? 0;
        SelectedSystems.Clear();
        foreach (var group in SelectedPlanets
                     .GroupBy(p => p.SolarSystemId)
                     .OrderBy(g => g.FirstOrDefault()?.SolarSystemName, StringComparer.OrdinalIgnoreCase))
        {
            var planets = group.OrderBy(p => p.PlanetId).ToList();
            SelectedSystems.Add(new PlanetarySelectedSystemItem
            {
                SolarSystemId = group.Key,
                Name = planets[0].SolarSystemName,
                Planets = planets,
            });
        }

        var match = selectedId > 0
            ? SelectedSystems.FirstOrDefault(p => p.SolarSystemId == selectedId)
            : null;
        _selectedSystemItem = match ?? SelectedSystems.FirstOrDefault();
        Notify(nameof(SelectedSystemItem));
        RefreshVisiblePlanets();
        Notify(nameof(HasSelectedSystems));
        Notify(nameof(HasNoSelectedSystems));
        Notify(nameof(SystemsHeaderText));
    }

    private void RefreshVisiblePlanets()
    {
        VisiblePlanets.Clear();
        var source = SelectedSystemItem?.Planets ?? (IReadOnlyList<PlanetaryPlanetOption>)SelectedPlanets;
        foreach (var planet in source)
        {
            VisiblePlanets.Add(planet);
        }
        Notify(nameof(HasVisiblePlanets));
        Notify(nameof(HasNoVisiblePlanets));
        Notify(nameof(PlanetListHeaderText));
    }

    private double _cycleDays = 4;
    public double CycleDays
    {
        get => _cycleDays;
        set
        {
            if (!Set(ref _cycleDays, Math.Max(0.1, value))) return;
            ScheduleSettingSave();
        }
    }

    private double _planetTaxPercent = 5;
    public double PlanetTaxPercent
    {
        get => _planetTaxPercent;
        set
        {
            if (!Set(ref _planetTaxPercent, Math.Max(0, Math.Min(100, value)))) return;
            ScheduleSettingSave();
        }
    }

    private double _salesTaxPercent = 3.6;
    public double SalesTaxPercent
    {
        get => _salesTaxPercent;
        set
        {
            if (!Set(ref _salesTaxPercent, Math.Max(0, Math.Min(100, value)))) return;
            ScheduleSettingSave();
        }
    }

    private double _brokerFeePercent;
    public double BrokerFeePercent
    {
        get => _brokerFeePercent;
        set
        {
            if (!Set(ref _brokerFeePercent, Math.Max(0, Math.Min(100, value)))) return;
            ScheduleSettingSave();
        }
    }

    private bool _useSetupCost = true;
    public bool UseSetupCost
    {
        get => _useSetupCost;
        set
        {
            if (!Set(ref _useSetupCost, value)) return;
            ScheduleSettingSave();
        }
    }

    private bool _useCurrentSupply = true;
    public bool UseCurrentSupply
    {
        get => _useCurrentSupply;
        set
        {
            if (!Set(ref _useCurrentSupply, value)) return;
            ScheduleSettingSave();
        }
    }

    private bool _useMarketPrices = true;
    public bool UseMarketPrices
    {
        get => _useMarketPrices;
        set
        {
            if (!Set(ref _useMarketPrices, value)) return;
            ScheduleSettingSave();
        }
    }

    private MarketLocation? _selectedPriceLocation;
    public MarketLocation? SelectedPriceLocation
    {
        get => _selectedPriceLocation;
        set
        {
            if (!Set(ref _selectedPriceLocation, value)) return;
            Notify(nameof(PriceLocationText));
            ScheduleSettingSave();
        }
    }

    public string PriceLocationText => SelectedPriceLocation is null
        ? LanguageService.Get("IndustryPlanetary_DefaultMarket")
        : LanguageService.Format("IndustryPlanetary_MarketWithRegion",
            SelectedPriceLocation.Name, SelectedPriceLocation.RegionId);

    private string _blueprintSearchText = string.Empty;
    public string BlueprintSearchText
    {
        get => _blueprintSearchText;
        set
        {
            if (!Set(ref _blueprintSearchText, value)) return;
            RefreshBlueprintResults();
        }
    }

    private PlanetaryBlueprintItem? _selectedBlueprint;
    public PlanetaryBlueprintItem? SelectedBlueprint
    {
        get => _selectedBlueprint;
        set
        {
            if (!Set(ref _selectedBlueprint, value)) return;
            Notify(nameof(SelectedBlueprintText));
        }
    }

    public string SelectedBlueprintText => SelectedBlueprint is null
        ? LanguageService.Get("IndustryPlanetary_NoBlueprint")
        : $"{SelectedBlueprint.ProductName} · {SelectedBlueprint.BlueprintName}";

    private double _blueprintQuantity = 1;
    public double BlueprintQuantity
    {
        get => _blueprintQuantity;
        set => Set(ref _blueprintQuantity, Math.Max(1, Math.Round(value)));
    }

    private string _marketStatusKey = "IndustryPlanetary_MarketLoading";
    private object[] _marketStatusArgs = Array.Empty<object>();

    public string MarketStatusText => LanguageService.Format(_marketStatusKey, _marketStatusArgs);

    private void SetMarketStatus(string key, params object[] args)
    {
        _marketStatusKey = key;
        _marketStatusArgs = args;
        Notify(nameof(MarketStatusText));
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value)) return;
            Notify(nameof(IsNotBusy));
            Notify(nameof(IsAnyBusy));
            Notify(nameof(BusyNoticeText));
        }
    }

    public bool IsNotBusy => !IsBusy;
    public bool IsAnyBusy => IsBusy || IsLiveBusy;
    public string BusyNoticeText => IsBusy ? BusyText : IsLiveBusy ? LiveStatusText : string.Empty;

    private string _busyText = string.Empty;
    public string BusyText
    {
        get => _busyText;
        set
        {
            if (!Set(ref _busyText, value)) return;
            Notify(nameof(BusyNoticeText));
        }
    }

    private PlanetaryPlan? _currentPlan;
    public PlanetaryPlan? CurrentPlan
    {
        get => _currentPlan;
        set
        {
            if (!Set(ref _currentPlan, value)) return;
            Notify(nameof(HasResult));
            Notify(nameof(HasNoResult));
            Notify(nameof(HasWarnings));
            Notify(nameof(PlanetSourceText));
            Notify(nameof(TargetDaysText));
            Notify(nameof(PaybackDaysText));
            Notify(nameof(ShowPlanSaveButton));
            RefreshPlanWarnings();
            RebuildProductTree();
        }
    }

    public bool HasResult => CurrentPlan is not null;
    public bool HasNoResult => CurrentPlan is null;
    public bool HasWarnings => PlanWarnings.Count > 0;

    /// <summary>计划提示：Core 存的是 <c>@code␟args</c> 编码，这里按语言解码（旧快照的纯文本原样显示）。</summary>
    public ObservableCollection<string> PlanWarnings { get; } = new();

    private void RefreshPlanWarnings()
    {
        if (_productNameMap.Count == 0)
        {
            BuildProductNameMap();
        }

        PlanWarnings.Clear();
        foreach (var warning in CurrentPlan?.Warnings ?? new List<string>())
        {
            PlanWarnings.Add(DecodeWarning(warning));
        }
        Notify(nameof(HasWarnings));
    }

    private string DecodeWarning(string warning)
    {
        if (string.IsNullOrEmpty(warning) || warning[0] != '@')
        {
            return warning;
        }

        var parts = warning[1..].Split('\u001F');
        var code = parts[0];
        var args = parts.Skip(1).ToArray();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "p3" or "p4")
            {
                args[i] = LanguageService.Get(args[i] == "p4"
                    ? "IndustryPlanetary_FactoryHighTech"
                    : "IndustryPlanetary_FactoryAdvanced");
            }
            else if (_productNameMap.TryGetValue(args[i], out var localName))
            {
                args[i] = localName;
            }
        }

        if (code == "unsupportedPlanet" && args.Length > 1 && string.IsNullOrWhiteSpace(args[0]))
        {
            args[0] = LanguageService.Format("IndustryPlanetary_Warn_PlanetFallback", args[1]);
        }

        var key = "IndustryPlanetary_Warn_" + code;
        var format = LanguageService.Get(key);
        if (format == key)
        {
            return warning;
        }
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>目标完成天数（本地化单位）。</summary>
    public string TargetDaysText => CurrentPlan is null
        ? "—"
        : LanguageService.Format("IndustryPlanetary_DaysValue", CurrentPlan.Summary.TargetDays);

    /// <summary>回本天数（本地化单位）。</summary>
    public string PaybackDaysText => CurrentPlan is null
        ? "—"
        : LanguageService.Format("IndustryPlanetary_DaysValue", CurrentPlan.Summary.PaybackDays);

    /// <summary>「已选需求（N）」——属性而非 StringFormat，才能跟着语言切换。</summary>
    public string TargetsCountText => LanguageService.Format("IndustryPlanetary_TargetsCount", Targets.Count);

    // ---- 产物链：目标 → 配方输入逐层展开的依赖节点树（对齐制造计划的 BOM / 成本视图）----

    public ObservableCollection<PlanetaryProductNodeViewModel> ProductRoots { get; } = new();
    public ObservableCollection<PlanetaryProductNodeViewModel> FlatProductNodes { get; } = new();

    private PlanetaryProductNodeViewModel? _selectedProductNode;
    public PlanetaryProductNodeViewModel? SelectedProductNode
    {
        get => _selectedProductNode;
        set => Set(ref _selectedProductNode, value);
    }

    public bool HasProductNodes => FlatProductNodes.Count > 0;
    public bool HasNoProductNodes => FlatProductNodes.Count == 0;
    public string ProductNodeCountText => ProductRoots.Count == 0
        ? string.Empty
        : LanguageService.Format("IndustryPlanetary_ProductNodes", ProductRoots.Count, FlatProductNodes.Count);

    private PlanetarySavedPlan? _selectedSavedPlan;
    public PlanetarySavedPlan? SelectedSavedPlan
    {
        get => _selectedSavedPlan;
        set => Set(ref _selectedSavedPlan, value);
    }

    public bool HasSavedPlans => SavedPlans.Count > 0;
    public bool HasNoSavedPlans => SavedPlans.Count == 0;
    public string SavedPlanCountText => LanguageService.Format("IndustryPlanetary_SavedPlanCount", SavedPlans.Count);

    public string ProductCountText => $"{FilteredProducts.Count} / {Products.Count}";

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        IsBusy = true;
        BusyText = LanguageService.Get("IndustryPlanetary_LoadingData");
        try
        {
            var (recipes, blueprints) = await Task.Run(() => (
                PlanetaryRecipeCatalog.Load(),
                IndustryDbService.GetAllManufacturableProducts()));
            _recipes = recipes;
            _planetTypes = new PlanetTypeCatalog(recipes);
            _planner = new PlanetaryPlanner(recipes, _planetTypes);
            var blueprintIds = blueprints
                .SelectMany(p => new[] { p.ProductTypeId, p.BlueprintTypeId })
                .Where(p => p > 0)
                .Distinct()
                .ToList();
            var localNames = new Dictionary<int, string>();
            if (TheGuideToTheNewEden.Core.Config.NeedLocalization && DBService.LocalDbReady)
            {
                var locals = LocalDbService.TranInvTypes(blueprintIds);
                if (locals != null)
                {
                    foreach (var local in locals)
                    {
                        if (local.TypeID > 0 && !string.IsNullOrWhiteSpace(local.TypeName))
                        {
                            localNames[local.TypeID] = local.TypeName;
                        }
                    }
                }
            }
            _blueprintItems = blueprints.Select(p => new PlanetaryBlueprintItem(
                p,
                localNames.TryGetValue(p.ProductTypeId, out var productName) ? productName : p.ProductName,
                localNames.TryGetValue(p.BlueprintTypeId, out var blueprintName) ? blueprintName : p.BlueprintName)).ToList();
            foreach (var tier in new[]
                     {
                         PlanetaryProductTier.P1, PlanetaryProductTier.P2,
                         PlanetaryProductTier.P3, PlanetaryProductTier.P4,
                     })
            {
                foreach (var product in recipes.GetProducts(tier))
                {
                    Products.Add(new PlanetaryProductItem(product));
                }
            }
            RefreshFilteredProducts();
            ReloadSavedPlans();
            SetMarketStatus("IndustryPlanetary_MarketLoading");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Format("IndustryPlanetary_LoadFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void AddSelectedProduct()
    {
        if (SelectedProduct is null) return;
        AddTarget(SelectedProduct.TypeId, SelectedProduct.Name, (long)TargetQuantity);
    }

    public void AddTarget(int typeId, string name, long quantity)
    {
        if (typeId <= 0 || quantity <= 0) return;
        var existing = Targets.FirstOrDefault(p => p.TypeId == typeId);
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            Targets.Add(new IndustryTargetItem { TypeId = typeId, Name = name, Quantity = quantity });
        }
    }

    public void RemoveTarget(IndustryTargetItem target)
    {
        Targets.Remove(target);
    }

    public void ClearTargets()
    {
        Targets.Clear();
    }

    public void RefreshFilteredProducts()
    {
        var search = ProductSearchText?.Trim() ?? string.Empty;
        var list = string.IsNullOrEmpty(search)
            ? Products.ToList()
            : Products.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                                  || p.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        FilteredProducts.Clear();
        foreach (var item in list)
        {
            FilteredProducts.Add(item);
        }
        if (SelectedProduct is null || !FilteredProducts.Contains(SelectedProduct))
        {
            SelectedProduct = FilteredProducts.FirstOrDefault();
        }
        Notify(nameof(ProductCountText));
    }

    public void RefreshBlueprintResults()
    {
        BlueprintResults.Clear();
        var search = BlueprintSearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(search)) return;
        foreach (var item in _blueprintItems
                     .Where(p => p.SearchText.Contains(search, StringComparison.OrdinalIgnoreCase))
                     .Take(120))
        {
            BlueprintResults.Add(item);
        }
    }

    public void ImportBlueprint()
    {
        if (SelectedBlueprint is null)
        {
            PageNotifyService.Info(LanguageService.Get("IndustryPlanetary_SelectBlueprintFirst"));
            return;
        }
        var entry = SelectedBlueprint.Entry;
        var product = IndustryDbService.GetProducts(entry.BlueprintTypeId, entry.ActivityId)
            .FirstOrDefault(p => p.ProductTypeID == entry.ProductTypeId);
        var perRun = product?.Quantity ?? 1;
        var runs = Math.Max(1, (long)Math.Ceiling(BlueprintQuantity / Math.Max(1, perRun)));
        var materials = IndustryDbService.GetMaterials(entry.BlueprintTypeId, entry.ActivityId);
        var imported = 0;
        foreach (var material in materials)
        {
            var pi = _recipes?.GetProduct(material.MaterialTypeID);
            if (pi is null || pi.Tier == PlanetaryProductTier.P0) continue;
            AddTarget(pi.TypeId, PlanetaryNameLocalizer.TypeName(pi.TypeId, pi.Name), (long)material.Quantity * runs);
            imported++;
        }

        // 正常导入不给提示（右侧「已选需求」列表就是结果）；只有"蓝图中没有行星产物"这类异常才弹全局通知。
        if (imported == 0)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryPlanetary_NoPlanetaryInBlueprint"));
        }
    }

    public async Task RefreshPricesAsync(bool forceRefresh = false)
    {
        if (_recipes is null) return;
        var regionId = SelectedPriceLocation?.RegionId ?? 0;
        var stats = regionId > 0
            ? await IndustryMarketDataService.Current.GetMarketStatsAsync(regionId, forceRefresh)
            : new Dictionary<int, IndustryMarketStat>();
        var average = await IndustryMarketDataService.Current.GetAveragePricesAsync(forceRefresh);
        _prices.Clear();
        foreach (var pair in average)
        {
            _prices[pair.Key] = pair.Value;
        }
        foreach (var pair in stats)
        {
            var stat = pair.Value;
            var price = stat.SellMin > 0 ? stat.SellMin : stat.BuyMax > 0 ? stat.BuyMax : stat.AveragePrice;
            if (price > 0) _prices[pair.Key] = price;
        }
        SetMarketStatus(stats.Count > 0
            ? "IndustryPlanetary_MarketStats"
            : "IndustryPlanetary_MarketAverage", stats.Count, DateTime.Now);
    }

    public async Task CalculateAsync()
    {
        if (_planner is null)
        {
            await LoadAsync();
        }
        if (_planner is null) return;
        if (Targets.Count == 0)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryPlanetary_NoTargets"));
            return;
        }

        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;
        IsBusy = true;
        BusyText = LanguageService.Get(UseMarketPrices
            ? "IndustryPlanetary_LoadingPrices"
            : "IndustryPlanetary_Calculating");
        try
        {
            if (UseMarketPrices)
            {
                await RefreshPricesAsync(false);
            }
            token.ThrowIfCancellationRequested();

            var selectedCharacters = Characters.Where(p => p.IsSelected).ToList();
            var request = new PlanetaryPlanRequest
            {
                Targets = Targets.Select(p => new PlanetaryTarget { TypeId = p.TypeId, Quantity = p.QuantityLong }).ToList(),
                PeriodDays = PeriodDays,
                Mode = (PlanetaryPlanMode)ModeIndex,
                CcuLevel = (int)CcuLevel,
                DefaultMaxPlanets = (int)MaxPlanets,
                RadiusKm = RadiusKm,
                CycleDays = CycleDays,
                PlanetTaxRate = PlanetTaxPercent / 100.0,
                SalesTaxRate = SalesTaxPercent / 100.0,
                BrokerFeeRate = BrokerFeePercent / 100.0,
                CountSetupCost = UseSetupCost,
                CurrentSupplyPerDay = ModeIndex == 2 && UseCurrentSupply
                    ? _liveSupplyPerDay
                        .Where(p => p.Value > 0)
                        .ToDictionary(p => p.Key, p => (long)Math.Round(p.Value))
                    : null,
                Characters = selectedCharacters.Select(p => new PlanetaryCharacterInput
                {
                    CharacterId = p.CharacterId,
                    Name = p.DisplayName,
                    CcuLevel = p.CcuLevel,
                    MaxPlanets = p.MaxPlanets,
                }).ToList(),
                Price = typeId => _prices.TryGetValue(typeId, out var price) ? price : 0,
                Planets = SelectedPlanets.Select(p => new PlanetaryPlanetInput
                {
                    PlanetId = p.PlanetId,
                    Name = p.Name,
                    TypeId = p.TypeId,
                    TypeName = p.PlanetType,
                    RadiusKm = p.RadiusKm,
                    SolarSystemId = p.SolarSystemId,
                    SolarSystemName = p.SolarSystemName,
                }).ToList(),
            };

            var plan = await Task.Run(() => _planner.Plan(request), token);
            token.ThrowIfCancellationRequested();
            LocalizePlan(plan);
            CurrentPlan = plan;
            SectionTabIndex = 2;
            SaveSettings();
            PageNotifyService.Success(plan.Warnings.Count > 0
                ? LanguageService.Format("IndustryPlanetary_PlanDone", plan.Warnings.Count)
                : LanguageService.Get("IndustryPlanetary_PlanDoneNoNotes"));
        }
        catch (OperationCanceledException)
        {
            PageNotifyService.Info(LanguageService.Get("IndustryPlanetary_PlanCancelled"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Format("IndustryPlanetary_PlanFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Cancel()
    {
        _operationCts?.Cancel();
    }

    public void ReloadSavedPlans()
    {
        var selectedId = SelectedSavedPlan?.Id;
        SavedPlans.Clear();
        foreach (var plan in IndustryPlanetaryPlanService.Load().OrderByDescending(p => p.Updated))
        {
            SavedPlans.Add(plan);
        }
        SelectedSavedPlan = SavedPlans.FirstOrDefault(p => p.Id == selectedId) ?? SavedPlans.FirstOrDefault();
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
    }

    public void SaveCurrentPlan()
    {
        if (CurrentPlan is null)
        {
            PageNotifyService.Info(LanguageService.Get("IndustryPlanetary_SaveNeedResult"));
            return;
        }
        var saved = CreateSavedPlan(null);
        // 新保存的计划排在最前（列表口径：最新在最上面）。
        SavedPlans.Insert(0, saved);
        SelectedSavedPlan = saved;
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
        if (IndustryPlanetaryPlanService.Save(SavedPlans))
        {
            PageNotifyService.Success(LanguageService.Format("IndustryPlanetary_Saved", saved.Name));
        }
        else
        {
            PageNotifyService.Error(LanguageService.Get("IndustryPlanetary_SaveFailed"));
        }
    }

    /// <summary>重命名已保存计划（列表项双击触发）。</summary>
    public bool RenameSelectedPlan(string name)
    {
        var saved = SelectedSavedPlan;
        var trimmed = name?.Trim() ?? string.Empty;
        if (saved is null || trimmed.Length == 0)
        {
            return false;
        }

        saved.Name = trimmed;
        saved.Updated = DateTime.Now;
        if (!IndustryPlanetaryPlanService.Save(SavedPlans))
        {
            PageNotifyService.Error(LanguageService.Get("IndustryPlanetary_RenameFailed"));
            return false;
        }

        var id = saved.Id;        ReloadSavedPlans();
        SelectedSavedPlan = SavedPlans.FirstOrDefault(p => p.Id == id) ?? SavedPlans.FirstOrDefault();
        PageNotifyService.Success(LanguageService.Format("IndustryPlanetary_Renamed", trimmed));
        return true;
    }

    public void LoadSelectedPlan()
    {
        var saved = SelectedSavedPlan;
        if (saved is null) return;
        _suspendSettingSave = true;
        Targets.Clear();
        foreach (var target in saved.Targets)
        {
            var product = _recipes?.GetProduct(target.TypeId);
            Targets.Add(new IndustryTargetItem
            {
                TypeId = target.TypeId,
                Name = PlanetaryNameLocalizer.TypeName(target.TypeId, product?.Name ?? ("Type " + target.TypeId)),
                Quantity = target.Quantity,
            });
        }
        PeriodDays = saved.PeriodDays;
        ModeIndex = (int)saved.Mode;
        CcuLevel = saved.CcuLevel;
        MaxPlanets = saved.DefaultMaxPlanets;
        RadiusKm = saved.RadiusKm;
        CycleDays = saved.CycleDays;
        LoadSavedPlanets(saved.Planets);
        PlanetTaxPercent = saved.PlanetTaxPercent;
        SalesTaxPercent = saved.SalesTaxPercent;
        BrokerFeePercent = saved.BrokerFeePercent;
        UseSetupCost = saved.CountSetupCost;
        UseCurrentSupply = saved.UseCurrentSupply;
        var savedChars = saved.Characters ?? new List<PlanetaryCharacterInput>();
        foreach (var character in Characters)
        {
            character.IsSelected = false;
        }
        foreach (var savedCharacter in savedChars)
        {
            var character = Characters.FirstOrDefault(p => p.CharacterId == savedCharacter.CharacterId);
            if (character is null) continue;
            character.IsSelected = true;
            character.CcuLevel = savedCharacter.CcuLevel;
            character.MaxPlanets = savedCharacter.MaxPlanets;
        }
        CurrentPlan = saved.Result;
        SectionTabIndex = saved.Result is null ? 1 : 2;
        _suspendSettingSave = false;
        SaveSettings();
        if (saved.Result is null)
        {
            PageNotifyService.Info(LanguageService.Get("IndustryPlanetary_LoadedNoResult"));
        }
    }

    public void DeleteSelectedPlan()
    {
        var saved = SelectedSavedPlan;
        if (saved is null) return;
        SavedPlans.Remove(saved);
        SelectedSavedPlan = SavedPlans.FirstOrDefault();
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
        if (!IndustryPlanetaryPlanService.Save(SavedPlans))
        {
            PageNotifyService.Error(LanguageService.Get("IndustryPlanetary_DeleteFailed"));
        }
    }

    /// <summary>
    /// 按配方依赖重建产物链：目标产物为根，配方输入里「计划内生产」的挂子节点，
    /// 没有安排生产的输入（外购 / P0 原料）挂成叶子并标注需外购还是靠提取。
    /// </summary>
    private void RebuildProductTree()
    {
        ProductRoots.Clear();

        var plan = CurrentPlan;
        if (plan?.Products is { Count: > 0 })
        {
            var products = new Dictionary<int, PlanetaryProductPlan>();
            foreach (var product in plan.Products)
            {
                if (product.TypeId > 0) products[product.TypeId] = product;
            }

            var imports = new Dictionary<int, PlanetaryImportPlan>();
            foreach (var import in plan.Imports)
            {
                if (import.TypeId > 0) imports[import.TypeId] = import;
            }

            var periodDays = plan.Summary.PeriodDays;
            if (periodDays <= 0)
            {
                periodDays = plan.Products
                    .Where(p => p.RequiredPerDay > 0)
                    .Select(p => p.RequiredPerPeriod / p.RequiredPerDay)
                    .DefaultIfEmpty(1)
                    .Max();
            }
            periodDays = Math.Max(0.01, periodDays);

            var roots = plan.Products.Where(p => p.IsTarget).ToList();
            if (roots.Count == 0)
            {
                // 老快照没有 IsTarget 标记时，用「没有被任何产物当成输入」的最高层产物兜底。
                var consumed = new HashSet<int>();
                foreach (var product in plan.Products)
                {
                    var recipe = _recipes?.GetRecipe(product.TypeId);
                    if (recipe is null) continue;
                    foreach (var input in recipe.Inputs) consumed.Add(input.TypeID);
                }
                roots = plan.Products.Where(p => !consumed.Contains(p.TypeId)).ToList();
                if (roots.Count == 0) roots = plan.Products.Take(1).ToList();
            }

            foreach (var product in roots)
            {
                ProductRoots.Add(BuildProductNode(product, products, imports, periodDays, 0, new HashSet<int>()));
            }
        }

        RebuildFlatProductNodes();
    }

    private PlanetaryProductNodeViewModel BuildProductNode(
        PlanetaryProductPlan product,
        Dictionary<int, PlanetaryProductPlan> products,
        Dictionary<int, PlanetaryImportPlan> imports,
        double periodDays,
        int depth,
        HashSet<int> path)
    {
        var node = new PlanetaryProductNodeViewModel(product, depth);
        if (!path.Add(product.TypeId))
        {
            return node;
        }

        var recipe = _recipes?.GetRecipe(product.TypeId);
        if (recipe is not null)
        {
            var cycles = recipe.OutputQuantity > 0 ? product.RequiredPerPeriod / recipe.OutputQuantity : 0;
            foreach (var input in recipe.Inputs)
            {
                if (products.TryGetValue(input.TypeID, out var childPlan))
                {
                    node.Children.Add(BuildProductNode(childPlan, products, imports, periodDays, depth + 1, path));
                    continue;
                }

                var inputProduct = _recipes?.GetProduct(input.TypeID);
                var fallback = inputProduct?.Name
                               ?? IndustryDbService.GetTypeInfo(input.TypeID)?.TypeName
                               ?? "Type " + input.TypeID;
                imports.TryGetValue(input.TypeID, out var importPlan);
                node.Children.Add(new PlanetaryProductNodeViewModel(
                    input.TypeID,
                    PlanetaryNameLocalizer.TypeName(input.TypeID, fallback),
                    inputProduct?.Tier ?? PlanetaryProductTier.P0,
                    Math.Max(0, input.Quantity * cycles / periodDays),
                    importPlan,
                    depth + 1));
            }
        }

        path.Remove(product.TypeId);
        return node;
    }

    /// <summary>按展开状态重建扁平节点列表（ListBox + 缩进，避免 TreeView 把卡片挤窄）。</summary>
    private void RebuildFlatProductNodes()
    {
        var selected = SelectedProductNode;
        FlatProductNodes.Clear();
        foreach (var root in ProductRoots) AddVisibleProductNode(root);
        SelectedProductNode = selected is not null && FlatProductNodes.Contains(selected)
            ? selected
            : FlatProductNodes.FirstOrDefault();
        Notify(nameof(HasProductNodes));
        Notify(nameof(HasNoProductNodes));
        Notify(nameof(ProductNodeCountText));
    }

    private void AddVisibleProductNode(PlanetaryProductNodeViewModel node)
    {
        FlatProductNodes.Add(node);
        if (!node.IsExpanded) return;
        foreach (var child in node.Children) AddVisibleProductNode(child);
    }

    public void ToggleProductNode(PlanetaryProductNodeViewModel? node)
    {
        if (node is null || !node.HasChildren) return;
        node.IsExpanded = !node.IsExpanded;
        RebuildFlatProductNodes();
    }

    public void ExpandAllProductNodes()
    {
        SetProductNodesExpanded(ProductRoots, true);
        RebuildFlatProductNodes();
    }

    public void CollapseAllProductNodes()
    {
        SetProductNodesExpanded(ProductRoots, false);
        RebuildFlatProductNodes();
    }

    private static void SetProductNodesExpanded(IEnumerable<PlanetaryProductNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.HasChildren) continue;
            node.IsExpanded = expanded;
            SetProductNodesExpanded(node.Children, expanded);
        }
    }

    private void LocalizePlan(PlanetaryPlan plan)
    {
        foreach (var product in plan.Products)
        {
            product.Name = PlanetaryNameLocalizer.TypeName(product.TypeId, product.Name);
            product.PlanetType = PlanetaryNameLocalizer.PlanetType(product.PlanetType);
            var recipe = _recipes?.GetProduct(product.TypeId)?.Recipe;
            if (recipe is null) continue;
            if (product.Tier == PlanetaryProductTier.P1 && recipe.Inputs.Count > 0)
            {
                var input = recipe.Inputs[0];
                var perDay = product.RequiredPerDay * input.Quantity / recipe.OutputQuantity;
                product.Inputs = LanguageService.Format(
                    "IndustryPlanetary_P0Input",
                    PlanetaryNameLocalizer.TypeName(input.TypeID, "Type " + input.TypeID),
                    perDay);
            }
            else
            {
                product.Inputs = string.Join(
                    LanguageService.Get("IndustryPlanetary_ListSeparator"),
                    recipe.Inputs.Select(p =>
                        $"{PlanetaryNameLocalizer.TypeName(p.TypeID, "Type " + p.TypeID)} {p.Quantity:N0}"));
            }
        }
        foreach (var colony in plan.Colonies)
        {
            colony.ProductName = PlanetaryNameLocalizer.TypeName(colony.ProductTypeId, colony.ProductName);
            colony.PlanetType = PlanetaryNameLocalizer.PlanetType(colony.PlanetType);
        }
        foreach (var import in plan.Imports)
        {
            import.Name = PlanetaryNameLocalizer.TypeName(import.TypeId, import.Name);
        }
        plan.Summary.TargetText = string.Join(
            LanguageService.Get("IndustryPlanetary_ListSeparator"),
            Targets.Select(p => $"{p.Name} × {p.QuantityLong:N0}"));

        // 提示的两种形态：
        // ① 新格式 = 语言无关的 `@code␟args`（由 DecodeWarning 按语言键格式化，参数在这里换成当前语言名）；
        // ② 旧快照 = 纯文本，仍按"英文 SDE 名 → 本地化名"做一次替换。
        BuildProductNameMap();

        for (var i = 0; i < plan.Warnings.Count; i++)
        {
            var warning = plan.Warnings[i];
            if (warning.StartsWith('@'))
            {
                continue;
            }

            warning = warning
                .Replace("Temperate", PlanetaryNameLocalizer.PlanetType("Temperate"))
                .Replace("Barren", PlanetaryNameLocalizer.PlanetType("Barren"));
            foreach (var pair in _productNameMap.OrderByDescending(p => p.Key.Length))
            {
                warning = warning.Replace(pair.Key, pair.Value);
            }
            plan.Warnings[i] = warning;
        }
    }

    /// <summary>英文 SDE 名 → 当前语言名（含用户自选目标的显示名），用于翻译提示参数与旧快照文本。</summary>
    private Dictionary<string, string> _productNameMap = new(StringComparer.Ordinal);

    private void BuildProductNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_recipes is not null)
        {
            foreach (var product in _recipes.Products)
            {
                if (string.IsNullOrWhiteSpace(product.Name))
                {
                    continue;
                }

                var local = PlanetaryNameLocalizer.TypeName(product.TypeId, product.Name);
                if (!string.IsNullOrWhiteSpace(local)
                    && !string.Equals(local, product.Name, StringComparison.Ordinal))
                {
                    map[product.Name] = local;
                }
            }
        }

        foreach (var target in Targets)
        {
            var product = _recipes?.GetProduct(target.TypeId);
            if (product is null || string.IsNullOrWhiteSpace(product.Name))
            {
                continue;
            }
            if (!string.Equals(product.Name, target.Name, StringComparison.Ordinal))
            {
                map[product.Name] = target.Name;
            }
        }

        _productNameMap = map;
    }

    private PlanetarySavedPlan CreateSavedPlan(PlanetarySavedPlan? existing)
    {
        // 保存按钮不再让用户先起名：新计划用带秒的时间戳，之后再双击列表项重命名。
        var baseName = existing?.Name
            ?? LanguageService.Format("IndustryPlanetary_DefaultPlanName", DateTime.Now.ToString("MM-dd HH:mm:ss"));
        var name = baseName;
        var index = 2;
        while (existing is null && SavedPlans.Any(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
        {
            name = $"{baseName} ({index++})";
        }
        var saved = existing ?? new PlanetarySavedPlan();
        saved.Name = name;
        saved.Updated = DateTime.Now;
        saved.Targets = Targets.Select(p => new PlanetaryTarget { TypeId = p.TypeId, Quantity = p.QuantityLong }).ToList();
        saved.PeriodDays = PeriodDays;
        saved.Mode = (PlanetaryPlanMode)ModeIndex;
        saved.CcuLevel = (int)CcuLevel;
        saved.DefaultMaxPlanets = (int)MaxPlanets;
        saved.RadiusKm = RadiusKm;
        saved.CycleDays = CycleDays;
        saved.Planets = SelectedPlanets.Select(p => new PlanetaryPlanetInput
        {
            PlanetId = p.PlanetId,
            Name = p.Name,
            TypeId = p.TypeId,
            TypeName = p.PlanetType,
            RadiusKm = p.RadiusKm,
            SolarSystemId = p.SolarSystemId,
            SolarSystemName = p.SolarSystemName,
        }).ToList();
        saved.PlanetTaxPercent = PlanetTaxPercent;
        saved.SalesTaxPercent = SalesTaxPercent;
        saved.BrokerFeePercent = BrokerFeePercent;
        saved.CountSetupCost = UseSetupCost;
        saved.UseCurrentSupply = UseCurrentSupply;
        saved.Characters = Characters.Where(p => p.IsSelected)
            .Select(p => new PlanetaryCharacterInput
            {
                CharacterId = p.CharacterId,
                Name = p.DisplayName,
                CcuLevel = p.CcuLevel,
                MaxPlanets = p.MaxPlanets,
            })
            .ToList();
        saved.Result = CurrentPlan;
        saved.Summary = CurrentPlan is null
            ? LanguageService.Get("IndustryPlanetary_NoResultSnapshot")
            : LanguageService.Format("IndustryPlanetary_SummaryLine",
                  CurrentPlan.Summary.TargetText, CurrentPlan.Summary.TotalColonies)
              + $" · {CurrentPlan.Summary.SetupCost:N0} ISK";
        return saved;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
