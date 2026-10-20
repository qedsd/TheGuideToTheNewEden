using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>蓝图详情的统一行（材料 / 产物 / 技能）。</summary>
public sealed class IndustryDetailRow
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>「工业 → 蓝图浏览器」页：按产品名搜索可制造物品，查看材料 / 产物 / 技能。</summary>
public sealed class IndustryBlueprintPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<IndustryProductEntry> Results { get; } = new();

    public ObservableCollection<IndustryDetailRow> Details { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    private IndustryProductEntry? _selectedEntry;
    public IndustryProductEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (Set(ref _selectedEntry, value))
            {
                _ = LoadDetailsAsync(value);
            }
        }
    }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value);
    }

    private string _subtitle = string.Empty;
    public string Subtitle
    {
        get => _subtitle;
        private set => Set(ref _subtitle, value);
    }

    public async Task SearchAsync()
    {
        var text = SearchText?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var items = await Task.Run(() => IndustryDbService.SearchManufacturableProducts(text, 100));
            Results.Clear();
            foreach (var item in items)
            {
                Results.Add(item);
            }

            if (Results.Count == 0)
            {
                PageNotifyService.Info(Resolve("IndustryPage_NoSearchResult"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_SearchFailed"));
        }
    }

    private async Task LoadDetailsAsync(IndustryProductEntry? entry)
    {
        Details.Clear();
        if (entry is null)
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        Title = entry.ProductName ?? string.Empty;
        try
        {
            var data = await Task.Run(() => BuildDetails(entry));
            Subtitle = data.Subtitle;
            foreach (var row in data.Rows)
            {
                Details.Add(row);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_CalcFailed"));
        }
    }

    private static (string Subtitle, List<IndustryDetailRow> Rows) BuildDetails(IndustryProductEntry entry)
    {
        var materials = IndustryDbService.GetMaterials(entry.BlueprintTypeId, entry.ActivityId);
        var products = IndustryDbService.GetProducts(entry.BlueprintTypeId, entry.ActivityId);
        var skills = IndustryDbService.GetSkills(entry.BlueprintTypeId, entry.ActivityId);
        var activities = IndustryDbService.GetActivities(entry.BlueprintTypeId);

        int time = 0;
        foreach (var activity in activities)
        {
            if (activity.ActivityID == entry.ActivityId)
            {
                time = activity.Time;
                break;
            }
        }

        var ids = new HashSet<int>();
        foreach (var material in materials) ids.Add(material.MaterialTypeID);
        foreach (var product in products) ids.Add(product.ProductTypeID);
        foreach (var skill in skills) ids.Add(skill.SkillTypeID);
        var names = IndustryDbService.GetTypeInfos(ids).ToDictionary(p => p.TypeID, p => p.TypeName);

        string Name(int typeId)
            => names.TryGetValue(typeId, out var name) && !string.IsNullOrEmpty(name) ? name : typeId.ToString();

        var rows = new List<IndustryDetailRow>();
        foreach (var material in materials.OrderByDescending(p => p.Quantity))
        {
            rows.Add(new IndustryDetailRow
            {
                Kind = Resolve("IndustryBp_Kind_Material"),
                Name = Name(material.MaterialTypeID),
                Amount = material.Quantity.ToString("N0"),
            });
        }
        foreach (var product in products)
        {
            rows.Add(new IndustryDetailRow
            {
                Kind = Resolve("IndustryBp_Kind_Product"),
                Name = Name(product.ProductTypeID),
                Amount = product.Quantity.ToString("N0"),
                Note = product.Probability.HasValue ? product.Probability.Value.ToString("P1") : string.Empty,
            });
        }
        foreach (var skill in skills)
        {
            rows.Add(new IndustryDetailRow
            {
                Kind = Resolve("IndustryBp_Kind_Skill"),
                Name = Name(skill.SkillTypeID),
                Amount = "Lv " + skill.Level,
            });
        }

        var subtitle = string.Format(
            Resolve("IndustryBp_Subtitle"),
            entry.BlueprintName ?? "—",
            entry.GroupName ?? "—",
            entry.MaxProductionLimit,
            time);
        return (subtitle, rows);
    }

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
