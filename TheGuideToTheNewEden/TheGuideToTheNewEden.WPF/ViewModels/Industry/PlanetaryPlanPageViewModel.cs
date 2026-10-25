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
    public string Summary => $"CCU {CcuLevel} · 行星位 {MaxPlanets}";

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

public sealed partial class PlanetaryPlanPageViewModel : INotifyPropertyChanged
{
    private PlanetaryRecipeCatalog? _recipes;
    private PlanetTypeCatalog? _planetTypes;
    private PlanetaryPlanner? _planner;
    private IndustrySetting _setting;
    private List<PlanetaryBlueprintItem> _blueprintItems = new();
    private readonly Dictionary<int, double> _prices = new();
    private readonly Dictionary<int, double> _liveSupplyPerDay = new();
    private CancellationTokenSource? _operationCts;
    private bool _loaded;

    public PlanetaryPlanPageViewModel()
    {
        _setting = IndustrySettingService.Load();
        _selectedPriceLocation = _setting.PriceLocation ?? _setting.Location;
        _planetTaxPercent = _setting.PlanetTaxPercent;
        _salesTaxPercent = _setting.SalesTaxPercent;
        _brokerFeePercent = _setting.BrokerFeePercent;
        _ccuLevel = 5;
        _cycleDays = 4;
        _periodDays = 7;
        foreach (var character in CharacterStore.Characters)
        {
            Characters.Add(new PlanetaryCharacterItem(character, true));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PlanetaryProductItem> Products { get; } = new();
    public ObservableCollection<PlanetaryProductItem> FilteredProducts { get; } = new();
    public ObservableCollection<IndustryTargetItem> Targets { get; } = new();
    public ObservableCollection<PlanetaryBlueprintItem> BlueprintResults { get; } = new();
    public ObservableCollection<PlanetaryCharacterItem> Characters { get; } = new();
    public ObservableCollection<PlanetarySavedPlan> SavedPlans { get; } = new();

    private int _sectionTabIndex;
    public int SectionTabIndex { get => _sectionTabIndex; set => Set(ref _sectionTabIndex, value); }

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
            Notify(nameof(PeriodText));
        }
    }

    public string PeriodText => $"按 {PeriodDays:N0} 天需求量反推";

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            if (!Set(ref _modeIndex, Math.Max(0, Math.Min(2, value)))) return;
            Notify(nameof(ModeText));
        }
    }

    public string ModeText => ModeIndex switch
    {
        1 => "只排目标工厂，配方输入全部采购",
        2 => "自给生产，并用「我的殖民地」产出抵扣输入需求",
        _ => "从 P0 提取开始，自行生产每一层",
    };

    private double _ccuLevel = 5;
    public double CcuLevel
    {
        get => _ccuLevel;
        set => Set(ref _ccuLevel, Math.Max(0, Math.Min(5, Math.Round(value))));
    }

    private double _maxPlanets = 6;
    public double MaxPlanets
    {
        get => _maxPlanets;
        set => Set(ref _maxPlanets, Math.Max(1, Math.Min(6, Math.Round(value))));
    }

    private double _radiusKm;
    public double RadiusKm
    {
        get => _radiusKm;
        set => Set(ref _radiusKm, Math.Max(0, Math.Round(value)));
    }

    private double _cycleDays = 4;
    public double CycleDays
    {
        get => _cycleDays;
        set => Set(ref _cycleDays, Math.Max(0.1, value));
    }

    private double _planetTaxPercent = 5;
    public double PlanetTaxPercent
    {
        get => _planetTaxPercent;
        set => Set(ref _planetTaxPercent, Math.Max(0, Math.Min(100, value)));
    }

    private double _salesTaxPercent = 3.6;
    public double SalesTaxPercent
    {
        get => _salesTaxPercent;
        set => Set(ref _salesTaxPercent, Math.Max(0, Math.Min(100, value)));
    }

    private double _brokerFeePercent;
    public double BrokerFeePercent
    {
        get => _brokerFeePercent;
        set => Set(ref _brokerFeePercent, Math.Max(0, Math.Min(100, value)));
    }

    private bool _useSetupCost = true;
    public bool UseSetupCost
    {
        get => _useSetupCost;
        set => Set(ref _useSetupCost, value);
    }

    private bool _useCurrentSupply = true;
    public bool UseCurrentSupply
    {
        get => _useCurrentSupply;
        set => Set(ref _useCurrentSupply, value);
    }

    private bool _useMarketPrices = true;
    public bool UseMarketPrices
    {
        get => _useMarketPrices;
        set => Set(ref _useMarketPrices, value);
    }

    private MarketLocation? _selectedPriceLocation;
    public MarketLocation? SelectedPriceLocation
    {
        get => _selectedPriceLocation;
        set
        {
            if (!Set(ref _selectedPriceLocation, value)) return;
            Notify(nameof(PriceLocationText));
        }
    }

    public string PriceLocationText => SelectedPriceLocation is null
        ? "默认市场位置"
        : $"{SelectedPriceLocation.Name}（星域 {SelectedPriceLocation.RegionId}）";

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
        ? "未选择蓝图"
        : $"{SelectedBlueprint.ProductName} · {SelectedBlueprint.BlueprintName}";

    private double _blueprintQuantity = 1;
    public double BlueprintQuantity
    {
        get => _blueprintQuantity;
        set => Set(ref _blueprintQuantity, Math.Max(1, Math.Round(value)));
    }

    private string _blueprintImportStatus = "搜索蓝图后导入其直接材料中的行星产物。";
    public string BlueprintImportStatus
    {
        get => _blueprintImportStatus;
        set => Set(ref _blueprintImportStatus, value);
    }

    private string _marketStatusText = "尚未加载市场数据";
    public string MarketStatusText
    {
        get => _marketStatusText;
        set => Set(ref _marketStatusText, value);
    }

    private string _planStatusText = "添加目标后执行计划";
    public string PlanStatusText
    {
        get => _planStatusText;
        set => Set(ref _planStatusText, value);
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
            Notify(nameof(TargetSummary));
            Notify(nameof(ResultHeadline));
        }
    }

    public bool HasResult => CurrentPlan is not null;
    public bool HasNoResult => CurrentPlan is null;
    public bool HasWarnings => CurrentPlan?.Warnings.Count > 0;
    public string TargetSummary => Targets.Count == 0
        ? "未添加需求"
        : string.Join(" · ", Targets.Select(p => $"{p.Name} ×{p.QuantityLong:N0}"));
    public string ResultHeadline => CurrentPlan is null
        ? "尚无计算结果"
        : $"{CurrentPlan.Summary.TargetText} · {CurrentPlan.Summary.TotalColonies} 个殖民地";

    private string _planName = "行星计划";
    public string PlanName
    {
        get => _planName;
        set => Set(ref _planName, value);
    }

    private PlanetarySavedPlan? _selectedSavedPlan;
    public PlanetarySavedPlan? SelectedSavedPlan
    {
        get => _selectedSavedPlan;
        set
        {
            if (!Set(ref _selectedSavedPlan, value)) return;
            if (value is not null)
            {
                PlanName = value.Name;
            }
        }
    }

    private string _savePlanStatus = "执行计划后可保存参数与结果快照。";
    public string SavePlanStatus
    {
        get => _savePlanStatus;
        set => Set(ref _savePlanStatus, value);
    }

    public bool HasSavedPlans => SavedPlans.Count > 0;
    public bool HasNoSavedPlans => SavedPlans.Count == 0;
    public string SavedPlanCountText => $"{SavedPlans.Count} 个计划";

    public string ProductCountText => $"{FilteredProducts.Count} / {Products.Count}";

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        IsBusy = true;
        BusyText = "载入行星配方与行星类型...";
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
            MarketStatusText = "点击「刷新价格」或直接执行计划时加载市场参考价";
            BlueprintImportStatus = $"已载入 {_blueprintItems.Count:N0} 条制造蓝图，输入产物名搜索。";
            PlanStatusText = "选择目标、角色与模式后执行计划";
        }
        catch (Exception ex)
        {
            PlanStatusText = "载入失败：" + ex.Message;
            PageNotifyService.Error("行星数据载入失败：" + ex.Message);
            Core.Log.Error(ex);
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
        Notify(nameof(TargetSummary));
        PlanStatusText = $"已添加 {name} × {quantity:N0}";
    }

    public void RemoveTarget(IndustryTargetItem target)
    {
        Targets.Remove(target);
        Notify(nameof(TargetSummary));
    }

    public void ClearTargets()
    {
        Targets.Clear();
        Notify(nameof(TargetSummary));
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
        if (BlueprintResults.Count == 0)
        {
            BlueprintImportStatus = "没有匹配的制造蓝图";
        }
        else
        {
            BlueprintImportStatus = $"找到 {BlueprintResults.Count} 条，选择后导入。";
        }
    }

    public void ImportBlueprint()
    {
        if (SelectedBlueprint is null)
        {
            PageNotifyService.Info("请先搜索并选择一个蓝图");
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
        BlueprintImportStatus = imported == 0
            ? "该蓝图的直接材料中没有行星产物"
            : $"已导入 {imported} 种行星产物（{SelectedBlueprint.ProductName} × {BlueprintQuantity:N0}，{runs} 流程）";
        PlanStatusText = BlueprintImportStatus;
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
        MarketStatusText = stats.Count > 0
            ? $"已加载 {stats.Count:N0} 个物品的详细订单 · {DateTime.Now:HH:mm}"
            : $"已加载参考价（平均价）· {DateTime.Now:HH:mm}";
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
            PageNotifyService.Warning("请先添加至少一个行星产物或从蓝图导入需求");
            return;
        }

        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;
        IsBusy = true;
        BusyText = UseMarketPrices ? "加载市场价格并反推殖民地..." : "反推行星殖民地...";
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
            };

            var plan = await Task.Run(() => _planner.Plan(request), token);
            token.ThrowIfCancellationRequested();
            LocalizePlan(plan);
            CurrentPlan = plan;
            SectionTabIndex = 2;
            PlanStatusText = plan.Warnings.Count > 0
                ? $"计划完成，有 {plan.Warnings.Count} 条提示"
                : "计划完成";
            _setting.PriceLocation = SelectedPriceLocation;
            _setting.PlanetTaxPercent = PlanetTaxPercent;
            _setting.SalesTaxPercent = SalesTaxPercent;
            _setting.BrokerFeePercent = BrokerFeePercent;
            IndustrySettingService.Save(_setting);
            PageNotifyService.Success("行星计划已完成");
        }
        catch (OperationCanceledException)
        {
            PlanStatusText = "计算已停止";
            PageNotifyService.Info("行星计划计算已停止");
        }
        catch (Exception ex)
        {
            PlanStatusText = "计算失败：" + ex.Message;
            Core.Log.Error(ex);
            PageNotifyService.Error("行星计划计算失败：" + ex.Message);
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
        SavePlanStatus = SavedPlans.Count == 0
            ? "还没有已保存计划；执行计划后保存参数与结果快照。"
            : "读取已保存计划不会重新计算，直接显示保存时的结果。";
    }

    public void SaveCurrentPlan()
    {
        if (CurrentPlan is null)
        {
            PageNotifyService.Info("请先执行行星计划，再保存结果快照");
            return;
        }
        var saved = CreateSavedPlan(null);
        SavedPlans.Add(saved);
        IndustryPlanetaryPlanService.Save(SavedPlans);
        PlanName = saved.Name;
        SelectedSavedPlan = saved;
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
        SavePlanStatus = $"已保存「{saved.Name}」· {DateTime.Now:HH:mm}";
        PageNotifyService.Success("行星计划已保存");
    }

    public void OverwriteSelectedPlan()
    {
        if (CurrentPlan is null)
        {
            PageNotifyService.Info("请先执行行星计划，再覆盖保存");
            return;
        }
        if (SelectedSavedPlan is null)
        {
            SaveCurrentPlan();
            return;
        }
        var index = SavedPlans.IndexOf(SelectedSavedPlan);
        var saved = CreateSavedPlan(SelectedSavedPlan);
        if (index >= 0) SavedPlans[index] = saved; else SavedPlans.Add(saved);
        IndustryPlanetaryPlanService.Save(SavedPlans);
        SelectedSavedPlan = saved;
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
        SavePlanStatus = $"已覆盖「{saved.Name}」· {DateTime.Now:HH:mm}";
        PageNotifyService.Success("已覆盖保存");
    }

    public void LoadSelectedPlan()
    {
        var saved = SelectedSavedPlan;
        if (saved is null) return;
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
        PlanStatusText = saved.Result is null
            ? "已载入计划参数；该快照没有计算结果，请重新执行。"
            : $"已载入计划「{saved.Name}」的结果快照";
        SavePlanStatus = $"已载入「{saved.Name}」· {saved.Updated:yyyy-MM-dd HH:mm}";
        Notify(nameof(TargetSummary));
    }

    public void DeleteSelectedPlan()
    {
        var saved = SelectedSavedPlan;
        if (saved is null) return;
        SavedPlans.Remove(saved);
        IndustryPlanetaryPlanService.Save(SavedPlans);
        SelectedSavedPlan = SavedPlans.FirstOrDefault();
        Notify(nameof(HasSavedPlans));
        Notify(nameof(HasNoSavedPlans));
        Notify(nameof(SavedPlanCountText));
        SavePlanStatus = $"已删除「{saved.Name}」";
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
                product.Inputs = string.Format(
                    "{0} {1:N1}/天（P0，提取）",
                    PlanetaryNameLocalizer.TypeName(input.TypeID, "Type " + input.TypeID),
                    perDay);
            }
            else
            {
                product.Inputs = string.Join("、", recipe.Inputs.Select(p =>
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
        plan.Summary.TargetText = string.Join("、", Targets.Select(p => $"{p.Name} × {p.QuantityLong:N0}"));
        for (var i = 0; i < plan.Warnings.Count; i++)
        {
            var warning = plan.Warnings[i]
                .Replace("Temperate", PlanetaryNameLocalizer.PlanetType("Temperate"))
                .Replace("Barren", PlanetaryNameLocalizer.PlanetType("Barren"));
            foreach (var target in Targets)
            {
                var product = _recipes?.GetProduct(target.TypeId);
                if (product is null || string.Equals(product.Name, target.Name, StringComparison.Ordinal))
                {
                    continue;
                }
                warning = warning.Replace(product.Name, target.Name);
            }
            plan.Warnings[i] = warning;
        }
    }

    private PlanetarySavedPlan CreateSavedPlan(PlanetarySavedPlan? existing)
    {
        var name = string.IsNullOrWhiteSpace(PlanName)
            ? "行星计划 " + DateTime.Now.ToString("MM-dd HH:mm")
            : PlanName.Trim();
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
            ? "无结果快照"
            : $"{CurrentPlan.Summary.TargetText} · {CurrentPlan.Summary.TotalColonies} 殖民地"
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
