using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed class IndustryActivityItem
{
    public int ActivityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TimeSeconds { get; set; }
    public string TimeText => TimeSeconds > 0 ? FormatDuration(TimeSeconds) : "—";

    private static string FormatDuration(int seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m {span.Seconds}s";
    }
}

public sealed class IndustryRecipeRow
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>「工业 → 蓝图浏览器」页：产品/蓝图/活动/配方/回收/压缩。</summary>
public sealed class IndustryBlueprintPageViewModel : INotifyPropertyChanged
{
    public ObservableCollection<IndustryActivityItem> Activities { get; } = new();
    public ObservableCollection<IndustryRecipeRow> MaterialRows { get; } = new();
    public ObservableCollection<IndustryRecipeRow> ProductRows { get; } = new();
    public ObservableCollection<IndustryRecipeRow> SkillRows { get; } = new();

    public ObservableCollection<IndustryProductTreeCategory> ProductTree { get; } = new();
    private List<IndustryProductEntry> _allProducts = new();
    private bool _productsLoaded;

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

    private string _productCountText = "—";
    public string ProductCountText { get => _productCountText; private set => Set(ref _productCountText, value); }

    private IndustryProductEntry? _selectedEntry;
    public IndustryProductEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (Set(ref _selectedEntry, value))
            {
                _ = LoadActivitiesAsync(value);
            }
        }
    }

    private IndustryActivityItem? _selectedActivity;
    public IndustryActivityItem? SelectedActivity
    {
        get => _selectedActivity;
        set
        {
            if (Set(ref _selectedActivity, value))
            {
                _ = LoadDetailsAsync();
            }
        }
    }

    private string _title = string.Empty;
    public string Title { get => _title; private set => Set(ref _title, value); }

    private string _subtitle = string.Empty;
    public string Subtitle { get => _subtitle; private set => Set(ref _subtitle, value); }

    private string _blueprintName = string.Empty;
    public string BlueprintName { get => _blueprintName; private set => Set(ref _blueprintName, value); }

    private string _groupName = string.Empty;
    public string GroupName { get => _groupName; private set => Set(ref _groupName, value); }

    private string _maxRunsText = "—";
    public string MaxRunsText { get => _maxRunsText; private set => Set(ref _maxRunsText, value); }

    private bool _languageHooked;

    public async Task LoadAllProductsAsync()
    {
        if (!_languageHooked)
        {
            _languageHooked = true;
            LanguageService.LanguageChanged += async (_, _) =>
            {
                // 分类/组的"未分类/未分组"、产品计数、蓝图副标题都是生成时拼的
                LocalizeProductRows(_allProducts);
                ApplyProductFilter();
                if (SelectedEntry is not null)
                {
                    await LoadActivitiesAsync(SelectedEntry);
                }
            };
        }

        if (_productsLoaded)
        {
            ApplyProductFilter();
            return;
        }

        try
        {
            var products = await Task.Run(() => IndustryDbService.GetAllManufacturableProducts());
            LocalizeProductRows(products);
            _allProducts = products;
            _productsLoaded = true;
            ApplyProductFilter();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Get("IndustryPage_LoadFailed"));
        }
    }

    public async Task SearchAsync()
    {
        if (!_productsLoaded)
        {
            await LoadAllProductsAsync();
            return;
        }
        ApplyProductFilter();
    }

    public void SelectProduct(IndustryProductTreeItem? item)
    {
        if (item is null || item.TypeId <= 0) return;
        var entry = _allProducts.FirstOrDefault(p => p.ProductTypeId == item.TypeId && p.ActivityId == item.ActivityId)
            ?? _allProducts.FirstOrDefault(p => p.ProductTypeId == item.TypeId);
        if (entry is not null && !ReferenceEquals(entry, SelectedEntry))
        {
            SelectedEntry = entry;
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
                Name = string.IsNullOrEmpty(categoryGroup.Key.CategoryName)
                    ? LanguageService.Get("LinksPage_Uncategorized")
                    : categoryGroup.Key.CategoryName,
                IsExpanded = !string.IsNullOrEmpty(keyword),
            };
            foreach (var groupGroup in categoryGroup
                         .GroupBy(p => new { p.GroupId, p.GroupName })
                         .OrderBy(g => g.Key.GroupName))
            {
                var group = new IndustryProductTreeGroup
                {
                    GroupId = groupGroup.Key.GroupId,
                    Name = string.IsNullOrEmpty(groupGroup.Key.GroupName)
                        ? LanguageService.Get("MapTool_Sov_Ungrouped")
                        : groupGroup.Key.GroupName,
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
            ? LanguageService.Format("IndustryPage_ManufacturableCount", _allProducts.Count)
            : LanguageService.Format("IndustryPage_MatchCount", filtered.Count, _allProducts.Count);
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
            Core.Log.Error(ex);
        }
    }

    private static bool Contains(string? text, string keyword)
        => !string.IsNullOrEmpty(text) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private async Task LoadActivitiesAsync(IndustryProductEntry? entry)
    {
        Activities.Clear();
        MaterialRows.Clear();
        ProductRows.Clear();
        SkillRows.Clear();

        if (entry is null)
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            BlueprintName = string.Empty;
            GroupName = string.Empty;
            MaxRunsText = "—";
            return;
        }

        Title = entry.ProductName ?? string.Empty;
        // 行星开发产物在 SQL 里是"伪蓝图"（BlueprintTypeId = 0），名称由界面按语言补。
        BlueprintName = entry.BlueprintTypeId <= 0
            ? LanguageService.Get("IndustryBlueprint_PlanetaryIndustry")
            : entry.BlueprintName ?? "—";
        GroupName = entry.GroupName ?? "—";
        MaxRunsText = entry.MaxProductionLimit > 0
            ? entry.MaxProductionLimit.ToString("N0")
            : LanguageService.Get("IndustryBlueprint_BpoUnlimited");
        Subtitle = LanguageService.Format("IndustryBlueprint_Subtitle",
            BlueprintName, GroupName, MaxRunsText);

        try
        {
            var activities = await Task.Run(() => IndustryDbService.GetActivities(entry.BlueprintTypeId));
            foreach (var activity in activities)
            {
                Activities.Add(new IndustryActivityItem
                {
                    ActivityId = activity.ActivityID,
                    Name = ActivityName(activity.ActivityID),
                    TimeSeconds = activity.Time,
                });
            }
            if (Activities.Count == 0)
            {
                Activities.Add(new IndustryActivityItem
                {
                    ActivityId = entry.ActivityId,
                    Name = ActivityName(entry.ActivityId),
                });
            }

            SelectedActivity = Activities.FirstOrDefault(p => p.ActivityId == entry.ActivityId)
                ?? Activities.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Get("IndustryBlueprint_LoadFailed"));
        }
    }

    private async Task LoadDetailsAsync()
    {
        if (SelectedEntry is null || SelectedActivity is null) return;
        try
        {
            var entry = SelectedEntry;
            var activity = SelectedActivity;
            var data = await Task.Run(() => BuildDetails(entry, activity));
            MaterialRows.Clear();
            ProductRows.Clear();
            SkillRows.Clear();
            foreach (var row in data.Materials) MaterialRows.Add(row);
            foreach (var row in data.Products) ProductRows.Add(row);
            foreach (var row in data.Skills) SkillRows.Add(row);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Get("IndustryBlueprint_RecipeFailed"));
        }
    }

    private static (List<IndustryRecipeRow> Materials, List<IndustryRecipeRow> Products, List<IndustryRecipeRow> Skills) BuildDetails(
        IndustryProductEntry entry, IndustryActivityItem activity)
    {
        var materials = IndustryDbService.GetMaterials(entry.BlueprintTypeId, activity.ActivityId);
        var products = IndustryDbService.GetProducts(entry.BlueprintTypeId, activity.ActivityId);
        var skills = IndustryDbService.GetSkills(entry.BlueprintTypeId, activity.ActivityId);

        var ids = new HashSet<int>();
        foreach (var material in materials) ids.Add(material.MaterialTypeID);
        foreach (var product in products) ids.Add(product.ProductTypeID);
        foreach (var skill in skills) ids.Add(skill.SkillTypeID);
        var names = BuildNameMap(ids);

        string Name(int typeId)
            => names.TryGetValue(typeId, out var name) && !string.IsNullOrEmpty(name) ? name : typeId.ToString();

        var materialRows = materials
            .OrderByDescending(p => p.Quantity)
            .Select(p => new IndustryRecipeRow
            {
                TypeId = p.MaterialTypeID,
                Name = Name(p.MaterialTypeID),
                Amount = "×" + p.Quantity.ToString("N0"),
                Note = LanguageService.Get("IndustryBlueprint_PerRun"),
            })
            .ToList();

        var productRows = products
            .Select(p => new IndustryRecipeRow
            {
                TypeId = p.ProductTypeID,
                Name = Name(p.ProductTypeID),
                Amount = "×" + p.Quantity.ToString("N0"),
                Note = p.Probability.HasValue
                    ? LanguageService.Format("IndustryBlueprint_SuccessRate", p.Probability.Value)
                    : string.Empty,
            })
            .ToList();

        var skillRows = skills
            .Select(p => new IndustryRecipeRow
            {
                TypeId = p.SkillTypeID,
                Name = Name(p.SkillTypeID),
                Amount = "Lv " + p.Level,
            })
            .ToList();

        return (materialRows, productRows, skillRows);
    }

    private static string ActivityName(int activityId)
    {
        return activityId switch
        {
            IndustryActivity.Manufacturing => LanguageService.Get("IndustryActivity_Manufacturing"),
            IndustryActivity.ResearchTechnology => LanguageService.Get("IndustryActivity_ResearchTechnology"),
            IndustryActivity.ResearchTimeEfficiency => LanguageService.Get("IndustryActivity_ResearchTime"),
            IndustryActivity.ResearchMaterialEfficiency => LanguageService.Get("IndustryActivity_ResearchMaterial"),
            IndustryActivity.Copying => LanguageService.Get("IndustryActivity_Copying"),
            IndustryActivity.Duplicating => LanguageService.Get("IndustryActivity_Duplicating"),
            IndustryActivity.ReverseEngineering => LanguageService.Get("IndustryActivity_ReverseEngineering"),
            IndustryActivity.Invention => LanguageService.Get("IndustryActivity_Invention"),
            IndustryActivity.Reaction => LanguageService.Get("IndustryActivity_Reaction"),
            _ => LanguageService.Format("IndustryBlueprint_UnknownActivity", activityId),
        };
    }

    private static Dictionary<int, string> BuildNameMap(IEnumerable<int> typeIds)
    {
        var ids = typeIds.Where(p => p > 0).Distinct().ToList();
        var result = new Dictionary<int, string>();
        foreach (var info in IndustryDbService.GetTypeInfos(ids))
        {
            result[info.TypeID] = info.TypeName;
        }

        if (TheGuideToTheNewEden.Core.Config.NeedLocalization && DBService.LocalDbReady && ids.Count > 0)
        {
            try
            {
                foreach (var local in LocalDbService.TranInvTypes(ids))
                {
                    if (!string.IsNullOrWhiteSpace(local.TypeName))
                    {
                        result[local.TypeID] = local.TypeName;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        return result;
    }

    private static string Resolve(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
