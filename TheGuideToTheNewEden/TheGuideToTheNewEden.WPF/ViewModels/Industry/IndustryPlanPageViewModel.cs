using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Industry;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Industry;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>搜索窗返回的待添加目标。</summary>
public sealed class IndustryTargetDraft
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Quantity { get; set; }
}

public sealed class IndustryTargetItem : INotifyPropertyChanged
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;

    private double _quantity = 1;
    public double Quantity
    {
        get => _quantity;
        set
        {
            value = Math.Max(1, Math.Round(value));
            if (Math.Abs(_quantity - value) < 0.0001) return;
            _quantity = value;
            NotifyQuantity();
        }
    }

    public long QuantityLong => Math.Max(1, (long)Math.Round(_quantity));
    public string DisplayName => $"{Name} × {QuantityLong:N0}";
    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyQuantity()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuantityLong)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}

public sealed class IndustryCharacterItem : INotifyPropertyChanged
{
    public IndustryCharacterItem(AuthorizedCharacterData character, bool selected)
    {
        Character = character;
        _isSelected = selected;
    }

    public AuthorizedCharacterData Character { get; }
    public string DisplayName => Character.CharacterName;

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

public sealed class IndustryRigItem : INotifyPropertyChanged
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public double MePercent { get; set; }
    public double TePercent { get; set; }
    public string DisplayName => $"{CategoryName}  材料效率 {MePercent:0.##}% / 时间效率 {TePercent:0.##}%";
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class IndustryCostSlice
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Percent { get; set; }
    public string PercentText => $"{Percent:0.#}%";
}

public sealed class IndustryDecryptorOption
{
    public int? TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IndustryDecryptorInfo? Info { get; set; }
    public override string ToString() => Name;
}

public sealed class IndustryOverviewBuyLine
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double Total => UnitPrice * Quantity;
    public string QuantityText => $"{Quantity:N0}";
    public string UnitPriceText => UnitPrice > 0 ? $"{UnitPrice:N2}" : "—";
    public string TotalText => Total > 0 ? $"{Total:N0}" : "—";
}

/// <summary>「角色与市场」里可勾选的库存仓库。</summary>
public sealed class IndustryWarehouseSelection : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

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

/// <summary>已保存计划列表项。</summary>
public sealed class IndustrySavedPlanItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UpdatedText { get; set; } = string.Empty;
    public string TargetSummary { get; set; } = string.Empty;
}

/// <summary>「工业 → 制造计划」页：多目标、多角色、结构 rig、解密器、市场深度、可视化 BOM。</summary>
public sealed class IndustryPlanPageViewModel : INotifyPropertyChanged
{
    private const int MaxTreeNodes = 6000;

    private IndustrySetting _setting;
    private readonly Dictionary<int, int> _manualDecisions = new();
    private readonly HashSet<int> _blacklist = new();
    private IndustryBuildPlan? _lastPlan;
    private DateTime? _resultSavedAt;
    private bool _loaded;
    private bool _forceRefresh;

    public IndustryPlanPageViewModel()
    {
        _setting = IndustrySettingService.Load();
        _meLevel = _setting.DefaultME;
        _teLevel = _setting.DefaultTE;
        _selectedLocation = _setting.Location;
        _selectedPriceLocation = _setting.PriceLocation ?? _setting.Location;
        _facilityTaxPercent = _setting.FacilityTaxPercent;
        _spaceModifier = _setting.SpaceModifier;
        _rigMeBonusPercent = _setting.RigMeBonusPercent;
        _rigTeBonusPercent = _setting.RigTeBonusPercent;
        _useAssets = _setting.UseAssets;
        _useInProgressJobs = _setting.UseInProgressJobs;
        _useOwnedBlueprints = _setting.UseOwnedBlueprints;
        _useCharacterSkills = _setting.UseCharacterSkills;
        _useMarketDepth = _setting.UseMarketDepth;
        _applyMeToReactions = _setting.ApplyMeToReactions;
        _applyRigToReactions = _setting.ApplyRigToReactions;
        _inventionAttemptsOverride = _setting.InventionAttemptsOverride ?? 0;
        _inventionProbabilityOverridePercent = _setting.InventionProbabilityOverridePercent ?? 0;
        _inventionBpcRunsOverride = _setting.InventionBpcRunsOverride ?? 0;

        foreach (var pair in _setting.ManualDecisions ?? new Dictionary<int, int>())
        {
            _manualDecisions[pair.Key] = pair.Value;
        }
        foreach (var id in _setting.Blacklist ?? new List<int>())
        {
            _blacklist.Add(id);
        }
        foreach (var rig in _setting.RigBonuses ?? new List<IndustryRigSetting>())
        {
            RigBonuses.Add(new IndustryRigItem
            {
                CategoryId = rig.CategoryId,
                CategoryName = rig.CategoryName,
                MePercent = rig.MePercent,
                TePercent = rig.TePercent,
            });
        }

        var selectedCharacters = new HashSet<long>(_setting.SelectedCharacterIds ?? new List<long>());
        int index = 0;
        foreach (var character in CharacterStore.Characters)
        {
            bool selected = selectedCharacters.Count > 0
                ? selectedCharacters.Contains(character.CharacterID)
                : index == 0;
            Characters.Add(new IndustryCharacterItem(character, selected));
            index++;
        }

        RefreshWarehouseSelections(preserveCurrent: false);
        IndustryInventoryService.Changed += (_, _) => RefreshWarehouseSelections(preserveCurrent: true);
        ReloadSavedPlans();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ───────────── 目标 ─────────────

    public ObservableCollection<IndustryTargetItem> Targets { get; } = new();

    public string TargetSummary
    {
        get
        {
            if (_lastPlan is not null && _lastPlan.Targets.Count > 0)
            {
                return string.Join(" · ", _lastPlan.Targets.Select(t =>
                {
                    long output = t.Decision == IndustryDecision.Buy || t.QuantityProduced <= 0
                        ? t.QuantityNeeded
                        : t.QuantityProduced;
                    return output != t.QuantityNeeded
                        ? $"{t.TypeName} ×{t.QuantityNeeded:N0} → {output:N0}"
                        : $"{t.TypeName} ×{output:N0}";
                }));
            }
            return Targets.Count == 0
                ? Resolve("IndustryPage_NoTargets", "尚未添加目标")
                : string.Join(" · ", Targets.Select(p => $"{p.Name} ×{p.QuantityLong:N0}"));
        }
    }

    // ───────────── 常驻物品树 ─────────────

    public ObservableCollection<IndustryProductTreeCategory> ProductTree { get; } = new();

    private List<IndustryProductEntry> _allProducts = new();

    private string _productSearchText = string.Empty;
    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (Set(ref _productSearchText, value))
            {
                ApplyProductFilter();
            }
        }
    }

    private string _productCountText = string.Empty;
    public string ProductCountText
    {
        get => _productCountText;
        private set => Set(ref _productCountText, value);
    }

    // ───────────── 参数 ─────────────

    private double _meLevel;
    public double MeLevel { get => _meLevel; set => Set(ref _meLevel, value); }

    private double _teLevel;
    public double TeLevel { get => _teLevel; set => Set(ref _teLevel, value); }

    private MarketLocation? _selectedLocation;
    public MarketLocation? SelectedLocation { get => _selectedLocation; set => Set(ref _selectedLocation, value); }

    private MarketLocation? _selectedPriceLocation;
    public MarketLocation? SelectedPriceLocation { get => _selectedPriceLocation; set => Set(ref _selectedPriceLocation, value); }

    private double _facilityTaxPercent;
    public double FacilityTaxPercent { get => _facilityTaxPercent; set => Set(ref _facilityTaxPercent, value); }

    private double _spaceModifier;
    public double SpaceModifier { get => _spaceModifier; set => Set(ref _spaceModifier, value); }

    private double _rigMeBonusPercent;
    public double RigMeBonusPercent { get => _rigMeBonusPercent; set => Set(ref _rigMeBonusPercent, value); }

    private double _rigTeBonusPercent;
    public double RigTeBonusPercent { get => _rigTeBonusPercent; set => Set(ref _rigTeBonusPercent, value); }

    public ObservableCollection<IndustryRigItem> RigBonuses { get; } = new();
    public ObservableCollection<InvCategory> Categories { get; } = new();

    private InvCategory? _newRigCategory;
    public InvCategory? NewRigCategory { get => _newRigCategory; set => Set(ref _newRigCategory, value); }

    private double _newRigMePercent;
    public double NewRigMePercent { get => _newRigMePercent; set => Set(ref _newRigMePercent, value); }

    private double _newRigTePercent;
    public double NewRigTePercent { get => _newRigTePercent; set => Set(ref _newRigTePercent, value); }

    public ObservableCollection<IndustryDecryptorOption> Decryptors { get; } = new();

    private IndustryDecryptorOption? _selectedDecryptor;
    public IndustryDecryptorOption? SelectedDecryptor { get => _selectedDecryptor; set => Set(ref _selectedDecryptor, value); }

    private double _inventionAttemptsOverride;
    public double InventionAttemptsOverride { get => _inventionAttemptsOverride; set => Set(ref _inventionAttemptsOverride, value); }

    private double _inventionProbabilityOverridePercent;
    public double InventionProbabilityOverridePercent { get => _inventionProbabilityOverridePercent; set => Set(ref _inventionProbabilityOverridePercent, value); }

    private double _inventionBpcRunsOverride;
    public double InventionBpcRunsOverride { get => _inventionBpcRunsOverride; set => Set(ref _inventionBpcRunsOverride, value); }

    private bool _applyMeToReactions;
    public bool ApplyMeToReactions { get => _applyMeToReactions; set => Set(ref _applyMeToReactions, value); }

    private bool _applyRigToReactions;
    public bool ApplyRigToReactions { get => _applyRigToReactions; set => Set(ref _applyRigToReactions, value); }

    // ───────────── 角色 / 库存 / 市场 ─────────────

    public ObservableCollection<IndustryCharacterItem> Characters { get; } = new();

    private bool _useAssets;
    public bool UseAssets { get => _useAssets; set => Set(ref _useAssets, value); }

    private bool _useInProgressJobs;
    public bool UseInProgressJobs { get => _useInProgressJobs; set => Set(ref _useInProgressJobs, value); }

    private bool _useOwnedBlueprints;
    public bool UseOwnedBlueprints { get => _useOwnedBlueprints; set => Set(ref _useOwnedBlueprints, value); }

    private bool _useCharacterSkills;
    public bool UseCharacterSkills { get => _useCharacterSkills; set => Set(ref _useCharacterSkills, value); }

    private bool _useMarketDepth;
    public bool UseMarketDepth { get => _useMarketDepth; set => Set(ref _useMarketDepth, value); }

    // ───────────── 库存仓库 / 已保存计划 ─────────────

    public ObservableCollection<IndustryWarehouseSelection> Warehouses { get; } = new();

    public ObservableCollection<IndustrySavedPlanItem> SavedPlans { get; } = new();

    private IndustrySavedPlanItem? _selectedSavedPlan;
    public IndustrySavedPlanItem? SelectedSavedPlan
    {
        get => _selectedSavedPlan;
        set
        {
            if (Set(ref _selectedSavedPlan, value) && value is not null)
            {
                PlanName = value.Name;
            }
        }
    }

    private string _planName = string.Empty;
    public string PlanName { get => _planName; set => Set(ref _planName, value); }

    public bool HasWarehouses => Warehouses.Count > 0;

    private string _marketStatusText = string.Empty;
    public string MarketStatusText { get => _marketStatusText; set => Set(ref _marketStatusText, value); }

    private string _characterStatusText = string.Empty;
    public string CharacterStatusText { get => _characterStatusText; set => Set(ref _characterStatusText, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

    /// <summary>外层分区：0 计划设置 / 1 计算结果 / 2 已保存计划。</summary>
    private int _sectionTabIndex;
    public int SectionTabIndex { get => _sectionTabIndex; set => Set(ref _sectionTabIndex, value); }

    /// <summary>「计划设置」内的子页签：0 目标 / 1 工艺 / 2 建筑 / 3 角色与市场。</summary>
    private int _settingsTabIndex;
    public int SettingsTabIndex { get => _settingsTabIndex; set => Set(ref _settingsTabIndex, value); }

    /// <summary>「计算结果」内的子页签：0 概况 / 1 生产链。</summary>
    private int _resultTabIndex;
    public int ResultTabIndex { get => _resultTabIndex; set => Set(ref _resultTabIndex, value); }

    private string _busyText = string.Empty;
    public string BusyText { get => _busyText; set => Set(ref _busyText, value); }

    private double _busyProgressPercent;
    public double BusyProgressPercent { get => _busyProgressPercent; set => Set(ref _busyProgressPercent, value); }

    private bool _hasBusyProgress;
    public bool HasBusyProgress { get => _hasBusyProgress; set => Set(ref _hasBusyProgress, value); }

    // ───────────── 结果 ─────────────

    public ObservableCollection<IndustryPlanNodeViewModel> Roots { get; } = new();
    public ObservableCollection<IndustryPlanNodeViewModel> FlatNodes { get; } = new();
    public ObservableCollection<IndustryCostSlice> CostSlices { get; } = new();
    public ObservableCollection<IndustryOverviewBuyLine> OverviewBuyItems { get; } = new();

    private IndustryPlanNodeViewModel? _selectedOverviewTarget;
    public IndustryPlanNodeViewModel? SelectedOverviewTarget
    {
        get => _selectedOverviewTarget;
        set
        {
            if (Set(ref _selectedOverviewTarget, value))
            {
                RebuildOverviewBuyItems();
            }
        }
    }

    private IndustryPlanNodeViewModel? _selectedNode;
    public IndustryPlanNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => Set(ref _selectedNode, value);
    }

    private string _summaryTargets = "—";
    public string SummaryTargets { get => _summaryTargets; private set => Set(ref _summaryTargets, value); }

    private string _summaryRuns = "0";
    public string SummaryRuns { get => _summaryRuns; private set => Set(ref _summaryRuns, value); }

    private string _summaryJobCost = "—";
    public string SummaryJobCost { get => _summaryJobCost; private set => Set(ref _summaryJobCost, value); }

    private string _summaryDirectBuy = "—";
    public string SummaryDirectBuy { get => _summaryDirectBuy; private set => Set(ref _summaryDirectBuy, value); }

    private string _summaryFullBuild = "—";
    public string SummaryFullBuild { get => _summaryFullBuild; private set => Set(ref _summaryFullBuild, value); }

    private string _summaryCash = "—";
    public string SummaryCash { get => _summaryCash; private set => Set(ref _summaryCash, value); }

    private string _summaryStock = "—";
    public string SummaryStock { get => _summaryStock; private set => Set(ref _summaryStock, value); }

    private string _summaryRevenue = "—";
    public string SummaryRevenue { get => _summaryRevenue; private set => Set(ref _summaryRevenue, value); }

    private string _summaryProfit = "—";
    public string SummaryProfit { get => _summaryProfit; private set => Set(ref _summaryProfit, value); }

    private string _summaryBuyVolume = "—";
    public string SummaryBuyVolume { get => _summaryBuyVolume; private set => Set(ref _summaryBuyVolume, value); }

    private bool _hasPlan;
    public bool HasPlan { get => _hasPlan; private set => Set(ref _hasPlan, value); }

    private bool _hasProfit;
    public bool HasProfit { get => _hasProfit; private set => Set(ref _hasProfit, value); }

    public bool ProfitIsPositive => _lastPlan?.Summary.ProfitKnown == true && _lastPlan.Summary.Profit >= 0;

    // ───────────── 生命周期 ─────────────

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var decryptors = await Task.Run(() => IndustryDbService.GetDecryptorTypes());
            var none = new IndustryDecryptorOption
            {
                TypeId = null,
                Name = Resolve("IndustryPage_DecryptorNone", "无解密器"),
            };
            Decryptors.Add(none);
            foreach (var decryptor in decryptors)
            {
                Decryptors.Add(new IndustryDecryptorOption
                {
                    TypeId = decryptor.TypeID,
                    Name = $"{decryptor.Name}  ×{decryptor.ProbabilityMultiplier:0.0##} / ME{decryptor.MeModifier:+#;-#;0} / TE{decryptor.TeModifier:+#;-#;0} / run{decryptor.RunModifier:+#;-#;0}",
                    Info = decryptor,
                });
            }
            SelectedDecryptor = Decryptors.FirstOrDefault(p => p.TypeId == _setting.DecryptorTypeId) ?? none;

            var categories = await Task.Run(() => IndustryDbService.GetCategories());
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
            NewRigCategory = Categories.FirstOrDefault();
            await LoadProductTreeAsync();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ───────────── 目标动作 ─────────────

    /// <summary>批量添加目标；同一 TypeID 合并数量。</summary>
    public void AddTargets(IEnumerable<IndustryTargetDraft>? drafts)
    {
        if (drafts is null) return;
        foreach (var draft in drafts)
        {
            if (draft is null || draft.TypeId <= 0 || draft.Quantity <= 0) continue;
            var existing = Targets.FirstOrDefault(p => p.TypeId == draft.TypeId);
            if (existing is not null)
            {
                existing.Quantity += draft.Quantity;
            }
            else
            {
                var target = new IndustryTargetItem
                {
                    TypeId = draft.TypeId,
                    Name = draft.Name,
                    Quantity = draft.Quantity,
                };
                target.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TargetSummary));
                Targets.Add(target);
            }
        }
        OnPropertyChanged(nameof(TargetSummary));
    }

    /// <summary>加载全部可制造物品（一次 JOIN），按分类/组构建常驻树。</summary>
    private async Task LoadProductTreeAsync()
    {
        try
        {
            var products = await Task.Run(() => IndustryDbService.GetAllManufacturableProducts());
            LocalizeProductRows(products);
            _allProducts = products;
            ApplyProductFilter();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static void LocalizeProductRows(List<IndustryProductEntry> products)
    {
        if (!TheGuideToTheNewEden.Core.Config.NeedLocalization || !DBService.LocalDbReady || products.Count == 0)
        {
            return;
        }

        try
        {
            var typeIds = products.Select(p => p.ProductTypeId)
                .Concat(products.Select(p => p.BlueprintTypeId))
                .Where(p => p > 0)
                .Distinct()
                .ToList();
            var typeNames = LocalDbService.TranInvTypes(typeIds)
                .Where(p => !string.IsNullOrWhiteSpace(p.TypeName))
                .GroupBy(p => p.TypeID)
                .ToDictionary(g => g.Key, g => g.First().TypeName);

            var groupIds = products.Select(p => p.GroupId).Where(p => p > 0).Distinct().ToList();
            var groupNames = LocalDbService.TranInvGroups(groupIds)
                .Where(p => !string.IsNullOrWhiteSpace(p.GroupName))
                .GroupBy(p => p.GroupID)
                .ToDictionary(g => g.Key, g => g.First().GroupName);

            var categoryIds = products.Select(p => p.CategoryId).Where(p => p > 0).Distinct().ToList();
            var categoryNames = LocalDbService.TranInvCategories(categoryIds)
                .Where(p => !string.IsNullOrWhiteSpace(p.CategoryName))
                .GroupBy(p => p.CategoryID)
                .ToDictionary(g => g.Key, g => g.First().CategoryName);

            foreach (var product in products)
            {
                if (typeNames.TryGetValue(product.ProductTypeId, out var name)) product.ProductName = name;
                if (typeNames.TryGetValue(product.BlueprintTypeId, out var blueprintName)) product.BlueprintName = blueprintName;
                if (groupNames.TryGetValue(product.GroupId, out var groupName)) product.GroupName = groupName;
                if (categoryNames.TryGetValue(product.CategoryId, out var categoryName)) product.CategoryName = categoryName;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void ApplyProductFilter()
    {
        var keyword = ProductSearchText?.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _allProducts
            : _allProducts.Where(p =>
                Contains(p.ProductName, keyword)
                || Contains(p.BlueprintName, keyword)
                || Contains(p.GroupName, keyword)
                || Contains(p.CategoryName, keyword))
                .ToList();

        ProductTree.Clear();
        foreach (var categoryGroup in filtered
                     .GroupBy(p => new { p.CategoryId, p.CategoryName })
                     .OrderBy(g => g.Key.CategoryName))
        {
            var category = new IndustryProductTreeCategory
            {
                CategoryId = categoryGroup.Key.CategoryId,
                Name = string.IsNullOrEmpty(categoryGroup.Key.CategoryName) ? "未分类" : categoryGroup.Key.CategoryName,
                IsExpanded = !string.IsNullOrEmpty(keyword),
            };
            foreach (var groupGroup in categoryGroup
                         .GroupBy(p => new { p.GroupId, p.GroupName })
                         .OrderBy(g => g.Key.GroupName))
            {
                var group = new IndustryProductTreeGroup
                {
                    GroupId = groupGroup.Key.GroupId,
                    Name = string.IsNullOrEmpty(groupGroup.Key.GroupName) ? "未分组" : groupGroup.Key.GroupName,
                    IsExpanded = !string.IsNullOrEmpty(keyword),
                };
                foreach (var product in groupGroup.OrderBy(p => p.ProductName))
                {
                    group.Items.Add(new IndustryProductTreeItem
                    {
                        TypeId = product.ProductTypeId,
                        Name = product.ProductName,
                        BlueprintName = product.BlueprintName,
                        ActivityId = product.ActivityId,
                    });
                }
                category.Groups.Add(group);
            }
            ProductTree.Add(category);
        }

        ProductCountText = string.IsNullOrEmpty(keyword)
            ? $"{_allProducts.Count} 个可制造物品"
            : $"匹配 {filtered.Count} / {_allProducts.Count}";
    }

    private static bool Contains(string? text, string keyword)
        => !string.IsNullOrEmpty(text) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    public void AddProduct(IndustryProductTreeItem? product)
    {
        if (product is null || product.TypeId <= 0) return;
        AddTargets(new[]
        {
            new IndustryTargetDraft
            {
                TypeId = product.TypeId,
                Name = product.Name,
                Quantity = 1,
            },
        });
    }

    public void RemoveTarget(IndustryTargetItem? target)
    {
        if (target is null) return;
        Targets.Remove(target);
        OnPropertyChanged(nameof(TargetSummary));
    }

    public void ClearTargets()
    {
        Targets.Clear();
        OnPropertyChanged(nameof(TargetSummary));
    }

    public void AddRig()
    {
        if (NewRigCategory is null) return;
        var existing = RigBonuses.FirstOrDefault(p => p.CategoryId == NewRigCategory.CategoryID);
        if (existing is not null)
        {
            existing.MePercent = NewRigMePercent;
            existing.TePercent = NewRigTePercent;
            RigBonuses.Remove(existing);
            RigBonuses.Add(existing);
        }
        else
        {
            RigBonuses.Add(new IndustryRigItem
            {
                CategoryId = NewRigCategory.CategoryID,
                CategoryName = NewRigCategory.CategoryName,
                MePercent = NewRigMePercent,
                TePercent = NewRigTePercent,
            });
        }
    }

    public void RemoveRig(IndustryRigItem? rig)
    {
        if (rig is not null) RigBonuses.Remove(rig);
    }

    // ───────────── 计算 ─────────────

    private void BeginBusy(string text)
    {
        IsBusy = true;
        BusyText = text;
        BusyProgressPercent = 0;
        HasBusyProgress = false;
    }

    private void EndBusy()
    {
        IsBusy = false;
        BusyText = string.Empty;
        BusyProgressPercent = 0;
        HasBusyProgress = false;
    }

    private Action<int, int> CreateMarketPageProgress()
    {
        return (page, total) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                UpdateMarketPageProgress(page, total);
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() => UpdateMarketPageProgress(page, total)));
            }
        };
    }

    private void UpdateMarketPageProgress(int page, int total)
    {
        BusyText = total > 0
            ? string.Format(Resolve("IndustryPage_MarketPage", "拉取市场深度：第 {0}/{1} 页"), page, total)
            : string.Format(Resolve("IndustryPage_MarketPageUnknown", "拉取市场深度：第 {0} 页"), page);
        HasBusyProgress = total > 0;
        BusyProgressPercent = total > 0 ? Math.Min(100, page * 100.0 / total) : 0;
    }

    public async Task CalculateAsync()
    {
        if (Targets.Count == 0)
        {
            PageNotifyService.Warning(Resolve("IndustryPage_NoTarget", "请先添加目标产物"));
            return;
        }
        if (IsBusy)
        {
            return;
        }

        BeginBusy(Resolve("IndustryPage_Prepare", "准备工业数据..."));
        try
        {
            SaveSetting();

            var contexts = Characters.Where(p => p.IsSelected)
                .Select(p => new CharacterContext(p.Character))
                .ToList();
            var rigs = RigBonuses.Select(p => new IndustryRigBonus
            {
                CategoryID = p.CategoryId,
                MeBonus = p.MePercent / 100.0,
                TeBonus = p.TePercent / 100.0,
            }).ToList();

            BusyText = Resolve("IndustryPage_FetchData", "读取价格、成本指数与角色数据...");

            var attemptsOverride = InventionAttemptsOverride > 0
                ? (int)Math.Round(InventionAttemptsOverride)
                : (int?)null;
            var probabilityOverride = InventionProbabilityOverridePercent > 0
                ? Math.Clamp(InventionProbabilityOverridePercent / 100.0, 0.0001, 1.0)
                : (double?)null;
            var bpcRunsOverride = InventionBpcRunsOverride > 0
                ? (int)Math.Round(InventionBpcRunsOverride)
                : (int?)null;
            var marketPageProgress = CreateMarketPageProgress();

            var options = await IndustryOptionsFactory.BuildAsync(
                contexts,
                SelectedLocation?.SolarSystemId ?? 0,
                SelectedPriceLocation?.RegionId ?? SelectedLocation?.RegionId ?? 0,
                FacilityTaxPercent / 100.0,
                (int)Math.Round(MeLevel),
                (int)Math.Round(TeLevel),
                SpaceModifier,
                RigMeBonusPercent / 100.0,
                RigTeBonusPercent / 100.0,
                rigs,
                SelectedDecryptor?.TypeId,
                ApplyMeToReactions,
                ApplyRigToReactions,
                UseAssets,
                UseInProgressJobs,
                UseOwnedBlueprints,
                UseCharacterSkills,
                UseMarketDepth,
                _forceRefresh,
                attemptsOverride,
                probabilityOverride,
                bpcRunsOverride,
                marketPageProgress);
            _forceRefresh = false;

            options.Decisions = _manualDecisions.ToDictionary(
                p => p.Key,
                p => p.Value == 1 ? IndustryDecision.Buy : IndustryDecision.Build);
            options.Blacklist = new HashSet<int>(_blacklist);
            options.Hangar = _setting.VirtualHangar is null
                ? new Dictionary<int, long>()
                : new Dictionary<int, long>(_setting.VirtualHangar);
            ApplyWarehouseStock(options);

            BusyText = Resolve("IndustryPage_Expanding", "展开 BOM...");

            var targets = Targets.Select(p => new IndustryBuildTarget(p.TypeId, p.QuantityLong)).ToList();
            var plan = await Task.Run(() => new IndustrySolver(options).Solve(targets));

            FillPlan(plan, contexts.Count);
            PageNotifyService.Success(string.Format(
                Resolve("IndustryPage_CalcDone", "计算完成：{0} 个流程"),
                plan.Summary.BuildRuns.Values.Sum()));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(string.Format(
                Resolve("IndustryPage_CalcFailed", "计算失败：{0}"),
                ex.Message));
        }
        finally
        {
            EndBusy();
        }
    }

    private void ClearResult()
    {
        _lastPlan = null;
        _resultSavedAt = null;
        Roots.Clear();
        FlatNodes.Clear();
        CostSlices.Clear();
        OverviewBuyItems.Clear();
        SelectedNode = null;
        SelectedOverviewTarget = null;
        HasPlan = false;
        HasProfit = false;
        SummaryTargets = "—";
        SummaryRuns = "0";
        SummaryJobCost = "—";
        SummaryDirectBuy = "—";
        SummaryFullBuild = "—";
        SummaryCash = "—";
        SummaryStock = "—";
        SummaryRevenue = "—";
        SummaryProfit = "—";
        SummaryBuyVolume = "—";
        OnPropertyChanged(nameof(ProfitIsPositive));
        OnPropertyChanged(nameof(TargetSummary));
    }

    private void FillPlan(IndustryBuildPlan plan, int characterCount, DateTime? resultSavedAt = null)
    {
        _lastPlan = plan;
        _resultSavedAt = resultSavedAt ?? DateTime.Now;
        LocalizePlanNames(plan.Targets);
        OnPropertyChanged(nameof(TargetSummary));
        Roots.Clear();
        double totalCost = plan.Summary.SelfBuildCost;
        foreach (var target in plan.Targets)
        {
            Roots.Add(BuildNodeViewModel(target, 0, totalCost));
        }
        RebuildFlatNodes();
        SelectedNode = FlatNodes.FirstOrDefault();
        SelectedOverviewTarget = Roots.FirstOrDefault();
        HasPlan = Roots.Count > 0;

        var summary = plan.Summary;
        SummaryTargets = plan.Targets.Count.ToString();
        SummaryRuns = summary.BuildRuns.Values.Sum().ToString("N0");
        SummaryJobCost = summary.JobCostKnown ? summary.TotalJobCost.ToString("N0") : "—";
        SummaryDirectBuy = summary.ComparisonKnown ? summary.DirectBuyCost.ToString("N0") : "—";
        SummaryFullBuild = summary.SelfBuildCost > 0 ? summary.SelfBuildCost.ToString("N0") : "—";
        SummaryCash = summary.CashOutlay > 0 ? summary.CashOutlay.ToString("N0") : "—";
        SummaryStock = summary.StockValue > 0 ? summary.StockValue.ToString("N0") : "—";
        HasProfit = summary.ProfitKnown;
        SummaryRevenue = summary.ProfitKnown ? summary.Revenue.ToString("N0") + " ISK" : "—";
        SummaryProfit = summary.ProfitKnown ? $"{summary.Profit:N0} ISK ({summary.Margin:P0})" : "—";
        SummaryBuyVolume = summary.TotalBuyVolume > 0 ? summary.TotalBuyVolume.ToString("N0") + " m³" : "—";
        OnPropertyChanged(nameof(ProfitIsPositive));

        CostSlices.Clear();
        if (summary.SelfBuildCost > 0)
        {
            double material = Math.Max(0, summary.CashOutlay - summary.TotalJobCost);
            double job = Math.Max(0, summary.TotalJobCost);
            double stock = Math.Max(0, summary.StockValue);
            AddCostSlice("material", Resolve("IndustryPage_CostMaterial", "采购材料"), material, summary.SelfBuildCost);
            AddCostSlice("job", Resolve("IndustryPage_CostJob", "任务费用"), job, summary.SelfBuildCost);
            AddCostSlice("stock", Resolve("IndustryPage_CostStock", "库存抵扣"), stock, summary.SelfBuildCost);
        }

        MarketStatusText = UseMarketDepth
            ? string.Format(Resolve("IndustryPage_MarketDepthStatus", "市场深度 · {0:HH:mm}"), DateTime.Now)
            : Resolve("IndustryPage_MarketAverageStatus", "平均价（未启用市场深度）");
        CharacterStatusText = characterCount > 0
            ? string.Format(Resolve("IndustryPage_CharacterStatus", "{0} 个角色参与"), characterCount)
            : Resolve("IndustryPage_NoCharacterStatus", "未选择角色");
        SectionTabIndex = 1;
        ResultTabIndex = 0;
    }

    private void AddCostSlice(string kind, string name, double value, double total)
    {
        if (value <= 0 || total <= 0) return;
        CostSlices.Add(new IndustryCostSlice
        {
            Kind = kind,
            Name = name,
            Percent = value / total * 100,
        });
    }

    private IndustryPlanNodeViewModel BuildNodeViewModel(IndustryBuildNode node, int depth, double totalCost)
    {
        var vm = new IndustryPlanNodeViewModel(node)
        {
            CostSharePercent = totalCost > 0 ? node.FullBuildCost / totalCost * 100 : 0,
            IsExpanded = depth < 2,
            Depth = depth,
            IndentMargin = new Thickness(depth * 18, 0, 0, 0),
        };
        foreach (var input in node.Inputs)
        {
            if (vm.Children.Count >= MaxTreeNodes) break;
            vm.Children.Add(BuildNodeViewModel(input, depth + 1, totalCost));
        }
        return vm;
    }

    /// <summary>把求解结果里的英文 TypeName 换成启用的本地化库名称。</summary>
    private static void LocalizePlanNames(IEnumerable<IndustryBuildNode> targets)
    {
        if (!TheGuideToTheNewEden.Core.Config.NeedLocalization || !DBService.LocalDbReady)
        {
            return;
        }

        var nodes = new List<IndustryBuildNode>();
        void Walk(IndustryBuildNode? node)
        {
            if (node is null) return;
            nodes.Add(node);
            foreach (var child in node.Inputs) Walk(child);
        }
        foreach (var target in targets) Walk(target);
        var ids = nodes.Select(p => p.TypeID).Where(p => p > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        try
        {
            var local = LocalDbService.TranInvTypes(ids)
                .Where(p => !string.IsNullOrWhiteSpace(p.TypeName))
                .GroupBy(p => p.TypeID)
                .ToDictionary(g => g.Key, g => g.First().TypeName);
            foreach (var node in nodes)
            {
                if (local.TryGetValue(node.TypeID, out var name))
                {
                    node.TypeName = node.IsInvention ? name + "（发明）" : name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    /// <summary>按展开状态重建扁平树（ListBox 全宽卡片，避免 TreeView 缩进把内容挤窄）。</summary>
    private void RebuildFlatNodes()
    {
        FlatNodes.Clear();
        foreach (var root in Roots) AddFlat(root);
        if (SelectedNode is not null && !FlatNodes.Contains(SelectedNode))
        {
            SelectedNode = FlatNodes.FirstOrDefault();
        }
    }

    private void AddFlat(IndustryPlanNodeViewModel node)
    {
        FlatNodes.Add(node);
        if (!node.IsExpanded) return;
        foreach (var child in node.Children) AddFlat(child);
    }

    public void ToggleNode(IndustryPlanNodeViewModel? node)
    {
        if (node is null || !node.HasChildren) return;
        node.IsExpanded = !node.IsExpanded;
        RebuildFlatNodes();
    }

    // ───────────── 节点动作 ─────────────

    public void ForceBuySelected()
    {
        if (SelectedNode is null) return;
        _manualDecisions[SelectedNode.TypeId] = 1;
        _blacklist.Remove(SelectedNode.TypeId);
        _ = CalculateAsync();
    }

    public void ForceBuildSelected()
    {
        if (SelectedNode is null) return;
        _manualDecisions[SelectedNode.TypeId] = 0;
        _blacklist.Remove(SelectedNode.TypeId);
        _ = CalculateAsync();
    }

    public void BlacklistSelected()
    {
        if (SelectedNode is null) return;
        _blacklist.Add(SelectedNode.TypeId);
        _manualDecisions.Remove(SelectedNode.TypeId);
        _ = CalculateAsync();
    }

    public void ClearManualDecisions()
    {
        _manualDecisions.Clear();
        _blacklist.Clear();
        if (HasPlan) _ = CalculateAsync();
    }

    // ───────────── 刷新 ─────────────

    public async Task RefreshMarketAsync()
    {
        if (IsBusy)
        {
            return;
        }

        BeginBusy(Resolve("IndustryPage_RefreshMarket", "刷新市场价格与成本指数..."));
        try
        {
            await IndustryMarketDataService.Current.GetAdjustedPricesAsync(true);
            await IndustryMarketDataService.Current.GetAveragePricesAsync(false);
            await IndustryMarketDataService.Current.GetCostIndicesAsync(true);
            if (UseMarketDepth)
            {
                long regionId = SelectedPriceLocation?.RegionId ?? SelectedLocation?.RegionId ?? 0;
                if (regionId > 0)
                {
                    var marketPageProgress = CreateMarketPageProgress();
                    await IndustryMarketDataService.Current.GetMarketStatsAsync(regionId, true, marketPageProgress);
                }
            }
            _forceRefresh = true;
            MarketStatusText = string.Format(Resolve("IndustryPage_MarketRefreshed", "市场数据已刷新 · {0:HH:mm}"), DateTime.Now);
            PageNotifyService.Success(Resolve("IndustryPage_MarketRefreshedDone", "市场数据已刷新"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_RefreshFailed", "刷新失败"));
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task RefreshCharacterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        BeginBusy(Resolve("IndustryPage_RefreshCharacter", "刷新角色蓝图/资产/技能..."));
        try
        {
            var contexts = Characters.Where(p => p.IsSelected)
                .Select(p => new CharacterContext(p.Character))
                .ToList();
            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                BusyText = $"刷新角色 {i + 1}/{contexts.Count}：{context.Character.CharacterName}";
                await CharacterIndustryDataService.GetOwnedBlueprintsAsync(context, true);
                await CharacterIndustryDataService.GetInProgressOutputsAsync(context, true);
                await CharacterIndustryDataService.GetAssetQuantitiesAsync(context, true);
                await CharacterIndustryDataService.GetSkillLevelsAsync(context, true);
            }
            _forceRefresh = true;
            CharacterStatusText = string.Format(Resolve("IndustryPage_CharacterRefreshed", "角色数据已刷新 · {0:HH:mm}"), DateTime.Now);
            PageNotifyService.Success(Resolve("IndustryPage_CharacterRefreshedDone", "角色数据已刷新"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_RefreshFailed", "刷新失败"));
        }
        finally
        {
            EndBusy();
        }
    }

    // ───────────── 导出 ─────────────

    private void RebuildOverviewBuyItems()
    {
        OverviewBuyItems.Clear();
        foreach (var line in BuildBuyLines(SelectedOverviewTarget?.Model))
        {
            OverviewBuyItems.Add(line);
        }
    }

    private static List<IndustryOverviewBuyLine> BuildBuyLines(IndustryBuildNode? target)
    {
        var lines = new Dictionary<int, IndustryOverviewBuyLine>();
        void Visit(IndustryBuildNode? node)
        {
            if (node is null) return;
            if (node.Decision == IndustryDecision.Buy && node.QuantityToBuy > 0)
            {
                if (!lines.TryGetValue(node.TypeID, out var line))
                {
                    line = new IndustryOverviewBuyLine
                    {
                        TypeId = node.TypeID,
                        Name = node.TypeName,
                        UnitPrice = node.ReferenceUnitPrice,
                    };
                    lines[node.TypeID] = line;
                }
                line.Quantity += node.QuantityToBuy;
            }
            foreach (var input in node.Inputs)
            {
                Visit(input);
            }
        }
        Visit(target);
        return lines.Values
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void CopyTargetPurchaseList(IndustryPlanNodeViewModel? target)
    {
        if (target is null)
        {
            PageNotifyService.Info(Resolve("IndustryPage_SelectProduct", "请先选择左侧产物"));
            return;
        }
        var lines = BuildBuyLines(target.Model);
        if (lines.Count == 0)
        {
            PageNotifyService.Info(Resolve("IndustryPage_NothingToCopy", "该产物没有需要采购的物品"));
            return;
        }
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine,
                lines.Select(p => $"{p.Name}\t{p.Quantity:N0}")));
            PageNotifyService.Success(Resolve("IndustryPage_Copied", "采购清单已复制"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_CopyFailed", "复制失败"));
        }
    }

    public void ExportTargetCsv(IndustryPlanNodeViewModel? target)
    {
        if (target is null)
        {
            PageNotifyService.Info(Resolve("IndustryPage_SelectProduct", "请先选择左侧产物"));
            return;
        }
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"industry-target-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            };
            if (dialog.ShowDialog() != true) return;

            var builder = new StringBuilder();
            builder.AppendLine("Target,Item,Decision,Activity,Needed,Produced,Runs,ToBuy,JobCost,UnitPrice,FullCost,CashCost,Revenue,Profit,Margin");
            WriteNodeCsv(builder, target.Model, target.Name, 0);
            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            PageNotifyService.Success(Resolve("IndustryPage_Exported", "计划已导出"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_ExportFailed", "导出失败"));
        }
    }

    public void CopyPurchaseList()
    {
        if (_lastPlan is null || _lastPlan.Summary.BuyQuantities.Count == 0)
        {
            PageNotifyService.Info(Resolve("IndustryPage_NothingToCopy", "没有需要采购的物品"));
            return;
        }
        try
        {
            var buy = _lastPlan.Summary.BuyQuantities;
            var names = IndustryDbService.GetTypeInfos(buy.Keys).ToDictionary(p => p.TypeID, p => p.TypeName);
            var builder = new StringBuilder();
            foreach (var pair in buy.OrderByDescending(p => p.Value))
            {
                var name = names.TryGetValue(pair.Key, out var typeName) && !string.IsNullOrEmpty(typeName)
                    ? typeName
                    : pair.Key.ToString();
                builder.AppendLine(name + "\t" + pair.Value);
            }
            Clipboard.SetText(builder.ToString());
            PageNotifyService.Success(Resolve("IndustryPage_Copied", "采购清单已复制"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_CopyFailed", "复制失败"));
        }
    }

    public void ExportCsv()
    {
        if (_lastPlan is null)
        {
            PageNotifyService.Info(Resolve("IndustryPage_NothingToCopy", "没有可导出的计划"));
            return;
        }
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"industry-plan-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            };
            if (dialog.ShowDialog() != true) return;

            var builder = new StringBuilder();
            builder.AppendLine("Target,Item,Decision,Activity,Needed,Produced,Runs,ToBuy,JobCost,UnitPrice,FullCost,CashCost,Revenue,Profit,Margin");
            foreach (var target in _lastPlan.Targets)
            {
                WriteNodeCsv(builder, target, target.TypeName, 0);
            }
            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            PageNotifyService.Success(Resolve("IndustryPage_Exported", "计划已导出"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_ExportFailed", "导出失败"));
        }
    }

    private static void WriteNodeCsv(StringBuilder builder, IndustryBuildNode node, string targetName, int depth)
    {
        string name = new string(' ', depth * 2) + node.TypeName;
        string decision = node.IsInvention ? "Invention" : node.Decision == IndustryDecision.Buy ? "Buy" : "Build";
        builder.AppendLine(string.Join(",",
            Csv(targetName), Csv(name), Csv(decision), node.ActivityID.ToString(CultureInfo.InvariantCulture),
            node.QuantityNeeded.ToString(CultureInfo.InvariantCulture),
            node.QuantityProduced.ToString(CultureInfo.InvariantCulture),
            node.Runs.ToString(CultureInfo.InvariantCulture),
            node.QuantityToBuy.ToString(CultureInfo.InvariantCulture),
            node.JobCost.ToString("0.##", CultureInfo.InvariantCulture),
            node.ReferenceUnitPrice.ToString("0.##", CultureInfo.InvariantCulture),
            node.FullBuildCost.ToString("0.##", CultureInfo.InvariantCulture),
            node.BuildCost.ToString("0.##", CultureInfo.InvariantCulture),
            node.Revenue.ToString("0.##", CultureInfo.InvariantCulture),
            node.Profit.ToString("0.##", CultureInfo.InvariantCulture),
            node.Margin.ToString("0.####", CultureInfo.InvariantCulture)));
        foreach (var input in node.Inputs)
        {
            WriteNodeCsv(builder, input, targetName, depth + 1);
        }
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ───────────── 库存仓库 ─────────────

    private void RefreshWarehouseSelections(bool preserveCurrent)
    {
        var selected = new HashSet<string>(_setting.SelectedWarehouseIds ?? new List<string>());
        if (preserveCurrent)
        {
            foreach (var item in Warehouses.Where(p => p.IsSelected))
            {
                selected.Add(item.Id);
            }
        }

        Warehouses.Clear();
        foreach (var warehouse in IndustryInventoryService.Warehouses.OrderBy(p => p.Name))
        {
            var selection = new IndustryWarehouseSelection
            {
                Id = warehouse.Id,
                Name = warehouse.Name,
                Summary = $"{warehouse.Items.Count:N0} 种 · {warehouse.Items.Values.Sum():N0} 件",
                IsSelected = selected.Contains(warehouse.Id),
            };
            selection.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IndustryWarehouseSelection.IsSelected))
                {
                    PersistWarehouseSelection();
                }
            };
            Warehouses.Add(selection);
        }
        OnPropertyChanged(nameof(HasWarehouses));
    }

    private void PersistWarehouseSelection()
    {
        _setting.SelectedWarehouseIds = Warehouses.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        IndustrySettingService.Save(_setting);
    }

    private void ApplyWarehouseStock(IndustrySolverOptions options)
    {
        var stock = new Dictionary<int, long>();
        foreach (var selection in Warehouses.Where(p => p.IsSelected))
        {
            var warehouse = IndustryInventoryService.Find(selection.Id);
            if (warehouse?.Items is null) continue;
            foreach (var pair in warehouse.Items)
            {
                if (pair.Value <= 0) continue;
                stock[pair.Key] = stock.TryGetValue(pair.Key, out var existing) ? existing + pair.Value : pair.Value;
            }
        }

        var merged = options.Assets is null
            ? new Dictionary<int, long>()
            : new Dictionary<int, long>(options.Assets);
        foreach (var pair in stock)
        {
            merged[pair.Key] = merged.TryGetValue(pair.Key, out var existing) ? existing + pair.Value : pair.Value;
        }
        options.Assets = merged;
    }

    // ───────────── 已保存计划 ─────────────

    private void ReloadSavedPlans()
    {
        SavedPlans.Clear();
        foreach (var plan in IndustryPlanService.Load().OrderByDescending(p => p.Updated))
        {
            var names = plan.Targets.Take(3)
                .Select(p => ResolveTypeName(p.TypeId))
                .ToList();
            var suffix = plan.Targets.Count > names.Count ? "…" : string.Empty;
            var resultText = plan.Result is null
                ? "未保存结果"
                : $"含结果 {(plan.ResultSavedAt ?? plan.Updated):MM-dd HH:mm}";
            SavedPlans.Add(new IndustrySavedPlanItem
            {
                Id = plan.Id,
                Name = plan.Name,
                UpdatedText = plan.Updated.ToString("yyyy-MM-dd HH:mm"),
                TargetSummary = plan.Targets.Count == 0
                    ? $"空计划 · {resultText}"
                    : $"{plan.Targets.Count:N0} 个目标 · {string.Join("、", names)}{suffix} · {resultText}",
            });
        }
    }

    public void SaveCurrentPlan()
    {
        var name = PlanName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            PageNotifyService.Warning("请先填写计划名称");
            return;
        }
        if (Targets.Count == 0)
        {
            PageNotifyService.Warning("请先添加目标产物");
            return;
        }

        SaveSetting();
        var plans = IndustryPlanService.Load();
        var plan = plans.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        bool existed = plan is not null;
        if (plan is null)
        {
            plan = new IndustrySavedPlan { Name = name };
            plans.Add(plan);
        }

        FillPlanSnapshot(plan, name);
        IndustryPlanService.Save(plans);
        ReloadSavedPlans();
        SelectedSavedPlan = SavedPlans.FirstOrDefault(p => p.Id == plan.Id);
        PageNotifyService.Success(existed ? "已更新同名计划" : "计划已保存");
    }

    public void OverwriteSelectedPlan()
    {
        if (SelectedSavedPlan is null)
        {
            PageNotifyService.Warning("请先在左侧选择一个已保存计划");
            return;
        }

        var plans = IndustryPlanService.Load();
        var plan = plans.FirstOrDefault(p => p.Id == SelectedSavedPlan.Id);
        if (plan is null)
        {
            PageNotifyService.Warning("计划不存在，可能已被删除");
            ReloadSavedPlans();
            return;
        }

        SaveSetting();
        FillPlanSnapshot(plan, string.IsNullOrWhiteSpace(PlanName) ? plan.Name : PlanName.Trim());
        IndustryPlanService.Save(plans);
        ReloadSavedPlans();
        SelectedSavedPlan = SavedPlans.FirstOrDefault(p => p.Id == plan.Id);
        PageNotifyService.Success("已覆盖保存");
    }

    public void LoadSelectedPlan()
    {
        if (SelectedSavedPlan is null)
        {
            PageNotifyService.Warning("请先在左侧选择一个已保存计划");
            return;
        }

        var plan = IndustryPlanService.Load().FirstOrDefault(p => p.Id == SelectedSavedPlan.Id);
        if (plan is null)
        {
            PageNotifyService.Warning("计划不存在，可能已被删除");
            ReloadSavedPlans();
            return;
        }

        _setting = CloneSetting(plan.Settings);
        ApplySettingToUi();
        ClearResult();
        ClearTargets();
        AddTargets(plan.Targets.Select(p => new IndustryTargetDraft
        {
            TypeId = p.TypeId,
            Name = ResolveTypeName(p.TypeId),
            Quantity = p.Quantity,
        }));
        IndustrySettingService.Save(_setting);

        PlanName = plan.Name;
        OnPropertyChanged(nameof(TargetSummary));
        if (plan.Result is not null)
        {
            FillPlan(plan.Result, plan.CharacterCount, plan.ResultSavedAt ?? plan.Updated);
            MarketStatusText = $"保存时价格快照 · {(_resultSavedAt ?? plan.Updated):yyyy-MM-dd HH:mm}";
            PageNotifyService.Success("已载入计划与保存时的结果；重新计算会刷新为当前价格");
        }
        else
        {
            SectionTabIndex = 0;
            SettingsTabIndex = 0;
            PageNotifyService.Success("计划已载入，请点击「计算制造计划」");
        }
    }

    public void RenameSelectedPlan()
    {
        if (SelectedSavedPlan is null)
        {
            PageNotifyService.Warning("请先在左侧选择一个已保存计划");
            return;
        }

        var name = PlanName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            PageNotifyService.Warning("请先填写计划名称");
            return;
        }

        var plans = IndustryPlanService.Load();
        var plan = plans.FirstOrDefault(p => p.Id == SelectedSavedPlan.Id);
        if (plan is null) return;
        plan.Name = name;
        plan.Updated = DateTime.Now;
        IndustryPlanService.Save(plans);
        ReloadSavedPlans();
        SelectedSavedPlan = SavedPlans.FirstOrDefault(p => p.Id == plan.Id);
        PageNotifyService.Success("已重命名");
    }

    public void DeleteSelectedPlan()
    {
        if (SelectedSavedPlan is null)
        {
            PageNotifyService.Warning("请先在左侧选择一个已保存计划");
            return;
        }

        var plans = IndustryPlanService.Load();
        plans.RemoveAll(p => p.Id == SelectedSavedPlan.Id);
        IndustryPlanService.Save(plans);
        ReloadSavedPlans();
        SelectedSavedPlan = null;
        PageNotifyService.Info("已删除计划");
    }

    private void FillPlanSnapshot(IndustrySavedPlan plan, string name)
    {
        plan.Name = name;
        plan.Updated = DateTime.Now;
        plan.Targets = Targets.Select(p => new IndustrySavedTarget
        {
            TypeId = p.TypeId,
            Quantity = p.QuantityLong,
        }).ToList();
        plan.Settings = CloneSetting(_setting);
        plan.Result = _lastPlan;
        plan.ResultSavedAt = _lastPlan is null ? null : (_resultSavedAt ?? DateTime.Now);
        plan.CharacterCount = Characters.Count(p => p.IsSelected);
    }

    private static IndustrySetting CloneSetting(IndustrySetting setting)
        => JsonConvert.DeserializeObject<IndustrySetting>(JsonConvert.SerializeObject(setting)) ?? new IndustrySetting();

    private static string ResolveTypeName(int typeId)
    {
        try
        {
            return InvTypeService.QueryType(typeId)?.TypeName ?? typeId.ToString();
        }
        catch
        {
            return typeId.ToString();
        }
    }

    private void ApplySettingToUi()
    {
        MeLevel = _setting.DefaultME;
        TeLevel = _setting.DefaultTE;
        SelectedLocation = _setting.Location;
        SelectedPriceLocation = _setting.PriceLocation ?? _setting.Location;
        FacilityTaxPercent = _setting.FacilityTaxPercent;
        SpaceModifier = _setting.SpaceModifier;
        RigMeBonusPercent = _setting.RigMeBonusPercent;
        RigTeBonusPercent = _setting.RigTeBonusPercent;
        ApplyMeToReactions = _setting.ApplyMeToReactions;
        ApplyRigToReactions = _setting.ApplyRigToReactions;
        InventionAttemptsOverride = _setting.InventionAttemptsOverride ?? 0;
        InventionProbabilityOverridePercent = _setting.InventionProbabilityOverridePercent ?? 0;
        InventionBpcRunsOverride = _setting.InventionBpcRunsOverride ?? 0;
        UseAssets = _setting.UseAssets;
        UseInProgressJobs = _setting.UseInProgressJobs;
        UseOwnedBlueprints = _setting.UseOwnedBlueprints;
        UseCharacterSkills = _setting.UseCharacterSkills;
        UseMarketDepth = _setting.UseMarketDepth;

        RigBonuses.Clear();
        foreach (var rig in _setting.RigBonuses ?? new List<IndustryRigSetting>())
        {
            RigBonuses.Add(new IndustryRigItem
            {
                CategoryId = rig.CategoryId,
                CategoryName = rig.CategoryName,
                MePercent = rig.MePercent,
                TePercent = rig.TePercent,
            });
        }

        _manualDecisions.Clear();
        foreach (var pair in _setting.ManualDecisions ?? new Dictionary<int, int>())
        {
            _manualDecisions[pair.Key] = pair.Value;
        }
        _blacklist.Clear();
        foreach (var id in _setting.Blacklist ?? new List<int>())
        {
            _blacklist.Add(id);
        }

        if (Decryptors.Count > 0)
        {
            SelectedDecryptor = Decryptors.FirstOrDefault(p => p.TypeId == _setting.DecryptorTypeId)
                ?? Decryptors.FirstOrDefault();
        }

        var selectedCharacters = new HashSet<long>(_setting.SelectedCharacterIds ?? new List<long>());
        for (int i = 0; i < Characters.Count; i++)
        {
            var character = Characters[i];
            character.IsSelected = selectedCharacters.Count == 0
                ? i == 0
                : selectedCharacters.Contains(character.Character.CharacterID);
        }

        RefreshWarehouseSelections(preserveCurrent: false);
    }

    // ───────────── 设置 ─────────────

    private void SaveSetting()
    {
        _setting.SolarSystemId = SelectedLocation?.SolarSystemId ?? 0;
        _setting.Location = SelectedLocation;
        _setting.PriceLocation = SelectedPriceLocation;
        _setting.FacilityTaxPercent = FacilityTaxPercent;
        _setting.DefaultME = (int)Math.Round(MeLevel);
        _setting.DefaultTE = (int)Math.Round(TeLevel);
        _setting.SpaceModifier = SpaceModifier;
        _setting.RigMeBonusPercent = RigMeBonusPercent;
        _setting.RigTeBonusPercent = RigTeBonusPercent;
        _setting.RigBonuses = RigBonuses.Select(p => new IndustryRigSetting
        {
            CategoryId = p.CategoryId,
            CategoryName = p.CategoryName,
            MePercent = p.MePercent,
            TePercent = p.TePercent,
        }).ToList();
        _setting.DecryptorTypeId = SelectedDecryptor?.TypeId;
        _setting.InventionAttemptsOverride = InventionAttemptsOverride > 0 ? (int)Math.Round(InventionAttemptsOverride) : null;
        _setting.InventionProbabilityOverridePercent = InventionProbabilityOverridePercent > 0 ? InventionProbabilityOverridePercent : null;
        _setting.InventionBpcRunsOverride = InventionBpcRunsOverride > 0 ? (int)Math.Round(InventionBpcRunsOverride) : null;
        _setting.ApplyMeToReactions = ApplyMeToReactions;
        _setting.ApplyRigToReactions = ApplyRigToReactions;
        _setting.UseAssets = UseAssets;
        _setting.UseInProgressJobs = UseInProgressJobs;
        _setting.UseOwnedBlueprints = UseOwnedBlueprints;
        _setting.UseCharacterSkills = UseCharacterSkills;
        _setting.UseMarketDepth = UseMarketDepth;
        _setting.ManualDecisions = new Dictionary<int, int>(_manualDecisions);
        _setting.Blacklist = _blacklist.ToList();
        _setting.SelectedWarehouseIds = Warehouses.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        _setting.SelectedCharacterIds = Characters.Where(p => p.IsSelected)
            .Select(p => p.Character.CharacterID)
            .ToList();
        IndustrySettingService.Save(_setting);
    }

    // ───────────── 辅助 ─────────────

    private static string Resolve(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
