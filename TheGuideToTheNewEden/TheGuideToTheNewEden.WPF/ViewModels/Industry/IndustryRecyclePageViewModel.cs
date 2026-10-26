using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Industry;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed class IndustryRecycleBatchItem : INotifyPropertyChanged
{
    public int TypeID { get; set; }
    public string Name { get; set; } = string.Empty;
    private double _quantity = 1;
    public double Quantity
    {
        get => _quantity;
        set
        {
            value = Math.Max(0, value);
            if (Math.Abs(_quantity - value) < 0.0001) return;
            _quantity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
        }
    }

    private bool _isIncluded = true;
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (_isIncluded == value) return;
            _isIncluded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class IndustryRecyclePageViewModel : INotifyPropertyChanged
{
    public ObservableCollection<InvType> SearchResults { get; } = new();
    public ObservableCollection<IndustryRecycleBatchItem> Batch { get; } = new();
    public ObservableCollection<OreProcessingOutput> Outputs { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    private InvType? _selectedType;
    public InvType? SelectedType
    {
        get => _selectedType;
        set
        {
            if (Set(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(SelectedTypeName));
            }
        }
    }

    public string SelectedTypeName => SelectedType?.TypeName
        ?? LanguageService.Get("IndustryRecycle_NoItemSelected");

    private double _quantity = 100;
    public double Quantity
    {
        get => _quantity;
        set => Set(ref _quantity, value);
    }

    private double _yieldPercent = 50;
    public double YieldPercent
    {
        get => _yieldPercent;
        set => Set(ref _yieldPercent, value);
    }

    private string _pasteText = string.Empty;
    public string PasteText
    {
        get => _pasteText;
        set => Set(ref _pasteText, value);
    }

    private string _statusText = LanguageService.Get("IndustryRecycle_StatusHint");
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public bool HasBatch => Batch.Count > 0;
    public string BatchSummary => Batch.Count == 0
        ? LanguageService.Get("IndustryRecycle_EmptyBatch")
        : LanguageService.Format("IndustryRecycle_BatchSummary", Batch.Count,
            Batch.Sum(p => (long)Math.Round(p.Quantity)));

    public void Search()
    {
        SearchResults.Clear();
        var keyword = SearchText?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            StatusText = LanguageService.Get("IndustryRecycle_EnterName");
            return;
        }

        try
        {
            var results = InvTypeService.QueryByName(keyword, true)
                .Where(p => p.TypeID > 0 && !string.IsNullOrWhiteSpace(p.TypeName))
                .Take(80)
                .ToList();
            foreach (var item in results)
            {
                SearchResults.Add(item);
            }
            SelectedType = SearchResults.FirstOrDefault();
            StatusText = results.Count == 0
                ? LanguageService.Get("IndustryRecycle_NoMatch")
                : LanguageService.Format("IndustryRecycle_Found", results.Count);
        }
        catch (Exception ex)
        {
            StatusText = LanguageService.Format("IndustryRecycle_SearchFailed", ex.Message);
        }
    }

    public void AddSelected()
    {
        if (SelectedType == null)
        {
            StatusText = LanguageService.Get("IndustryRecycle_SelectItem");
            return;
        }
        AddBatchItem(SelectedType.TypeID, SelectedType.TypeName, (long)Math.Max(0, Math.Round(Quantity)));
        NotifyBatch();
    }

    public void ImportPaste()
    {
        if (string.IsNullOrWhiteSpace(PasteText))
        {
            StatusText = LanguageService.Get("IndustryRecycle_PasteEmpty");
            return;
        }

        var lines = AppraisalTextParser.Parse(PasteText);
        if (lines.Count == 0)
        {
            StatusText = LanguageService.Get("IndustryRecycle_NoLines");
            return;
        }

        var resolved = MarketTypeNameResolver.Resolve(lines.Select(p => p.Name));
        int added = 0;
        int unknown = 0;
        foreach (var line in lines)
        {
            if (!resolved.TryGetValue(line.Name, out var type) || type == null)
            {
                unknown++;
                continue;
            }
            long quantity = (long)Math.Max(0, Math.Round(line.Amount));
            if (quantity <= 0) continue;
            AddBatchItem(type.TypeID, type.TypeName, quantity);
            added++;
        }
        NotifyBatch();
        StatusText = LanguageService.Format("IndustryRecycle_Imported", added)
            + (unknown > 0
                ? LanguageService.Format("IndustryRecycle_ImportedUnknown", 0, unknown)
                : LanguageService.Get("IndustryRecycle_ImportedDone"));
        if (added > 0) PasteText = string.Empty;
    }

    public void ClearBatch()
    {
        Batch.Clear();
        Outputs.Clear();
        NotifyBatch();
        StatusText = LanguageService.Get("IndustryRecycle_Cleared");
    }

    public void Reprocess()
    {
        Outputs.Clear();
        var items = Batch.Where(p => p.IsIncluded && p.Quantity > 0).ToList();
        if (items.Count == 0)
        {
            StatusText = LanguageService.Get("IndustryRecycle_SelectItems");
            return;
        }

        try
        {
            var merged = new Dictionary<int, OreProcessingOutput>();
            foreach (var item in items)
            {
                var results = IndustryOreCalculator.Reprocess(
                    item.TypeID, (long)Math.Round(item.Quantity), Math.Clamp(YieldPercent / 100.0, 0, 1));
                foreach (var result in results)
                {
                    if (merged.TryGetValue(result.TypeID, out var existing))
                    {
                        existing.Quantity += result.Quantity;
                    }
                    else
                    {
                        merged[result.TypeID] = result;
                    }
                }
            }

            foreach (var output in merged.Values.OrderBy(p => p.Name))
            {
                Outputs.Add(output);
            }
            StatusText = merged.Count == 0
                ? LanguageService.Get("IndustryRecycle_NoReprocess")
                : LanguageService.Format("IndustryRecycle_ReprocessDone",
                    items.Count, merged.Count, YieldPercent);
        }
        catch (Exception ex)
        {
            StatusText = LanguageService.Format("IndustryRecycle_ReprocessFailed", ex.Message);
        }
    }

    public void Compress()
    {
        Outputs.Clear();
        var items = Batch.Where(p => p.IsIncluded && p.Quantity > 0).ToList();
        if (items.Count == 0)
        {
            StatusText = LanguageService.Get("IndustryRecycle_SelectItems");
            return;
        }

        try
        {
            int skipped = 0;
            foreach (var item in items)
            {
                var result = IndustryOreCalculator.Compress(item.TypeID, (long)Math.Round(item.Quantity));
                if (result == null)
                {
                    skipped++;
                    continue;
                }
                Outputs.Add(result);
            }
            StatusText = Outputs.Count == 0
                ? LanguageService.Get("IndustryRecycle_NoCompressMapping")
                : LanguageService.Format("IndustryRecycle_CompressDone", Outputs.Count)
                  + (skipped > 0
                      ? LanguageService.Format("IndustryRecycle_CompressSkipped", 0, skipped)
                      : LanguageService.Get("IndustryRecycle_ImportedDone"));
        }
        catch (Exception ex)
        {
            StatusText = LanguageService.Format("IndustryRecycle_CompressFailed", ex.Message);
        }
    }

    private void AddBatchItem(int typeId, string name, long quantity)
    {
        if (typeId <= 0 || quantity <= 0) return;
        var existing = Batch.FirstOrDefault(p => p.TypeID == typeId);
        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            Batch.Add(new IndustryRecycleBatchItem
            {
                TypeID = typeId,
                Name = name,
                Quantity = quantity,
            });
        }
    }

    private void NotifyBatch()
    {
        OnPropertyChanged(nameof(HasBatch));
        OnPropertyChanged(nameof(BatchSummary));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
