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

    public async Task LoadAllProductsAsync()
    {
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
            PageNotifyService.Error(Resolve("IndustryPage_LoadFailed", "读取物品失败"));
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
        BlueprintName = entry.BlueprintName ?? "—";
        GroupName = entry.GroupName ?? "—";
        MaxRunsText = entry.MaxProductionLimit > 0 ? entry.MaxProductionLimit.ToString("N0") : "BPO / 不限";
        Subtitle = $"{BlueprintName} · {GroupName} · 最大流程 {MaxRunsText}";

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
            PageNotifyService.Error(Resolve("IndustryPage_CalcFailed", "读取蓝图失败"));
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
            PageNotifyService.Error(Resolve("IndustryPage_CalcFailed", "读取配方失败"));
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
                Note = "每流程",
            })
            .ToList();

        var productRows = products
            .Select(p => new IndustryRecipeRow
            {
                TypeId = p.ProductTypeID,
                Name = Name(p.ProductTypeID),
                Amount = "×" + p.Quantity.ToString("N0"),
                Note = p.Probability.HasValue ? $"成功率 {p.Probability.Value:P1}" : string.Empty,
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
            IndustryActivity.Manufacturing => Resolve("IndustryActivity_Manufacturing", "制造"),
            IndustryActivity.ResearchTechnology => Resolve("IndustryActivity_ResearchTechnology", "科技研究"),
            IndustryActivity.ResearchTimeEfficiency => Resolve("IndustryActivity_ResearchTime", "时间效率研究"),
            IndustryActivity.ResearchMaterialEfficiency => Resolve("IndustryActivity_ResearchMaterial", "材料效率研究"),
            IndustryActivity.Copying => Resolve("IndustryActivity_Copying", "复制"),
            IndustryActivity.Duplicating => Resolve("IndustryActivity_Duplicating", "复制"),
            IndustryActivity.ReverseEngineering => Resolve("IndustryActivity_ReverseEngineering", "逆向工程"),
            IndustryActivity.Invention => Resolve("IndustryActivity_Invention", "发明"),
            IndustryActivity.Reaction => Resolve("IndustryActivity_Reaction", "反应"),
            _ => "Activity " + activityId,
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
