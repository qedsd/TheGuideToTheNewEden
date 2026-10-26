using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Industry;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>库存页左侧的仓库条目。</summary>
public sealed class IndustryWarehouseItemVm : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; Notify(); }
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set { if (_summary == value) return; _summary = value; Notify(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>库存明细行。</summary>
public sealed class IndustryInventoryItemView
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public double Volume { get; set; }
    public string QuantityText => Quantity.ToString("N0");
    public string VolumeText => Volume > 0 ? Volume.ToString("N0") + " m³" : "—";
}

/// <summary>可导入资产的角色条目。</summary>
public sealed class IndustryCharacterImportItem : INotifyPropertyChanged
{
    public IndustryCharacterImportItem(AuthorizedCharacterData character)
    {
        Character = character;
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

/// <summary>「工业 → 库存」页：多仓库管理、手动添加、角色导入、CSV 导入/导出。</summary>
public sealed class IndustryInventoryPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<IndustryWarehouseItemVm> Warehouses { get; } = new();

    public ObservableCollection<IndustryInventoryItemView> Items { get; } = new();

    public ObservableCollection<DataBaseSearchItem> SearchResults { get; } = new();

    public ObservableCollection<IndustryCharacterImportItem> Characters { get; } = new();

    private IndustryWarehouseItemVm? _selectedWarehouse;
    public IndustryWarehouseItemVm? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (Set(ref _selectedWarehouse, value))
            {
                NewWarehouseName = value?.Name ?? string.Empty;
                RefreshItems();
            }
        }
    }

    private string _newWarehouseName = string.Empty;
    public string NewWarehouseName
    {
        get => _newWarehouseName;
        set => Set(ref _newWarehouseName, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    private DataBaseSearchItem? _selectedSearchResult;
    public DataBaseSearchItem? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set => Set(ref _selectedSearchResult, value);
    }

    private double _addQuantity = 1;
    public double AddQuantity
    {
        get => _addQuantity;
        set => Set(ref _addQuantity, value);
    }

    private double _editQuantity = 1;
    public double EditQuantity
    {
        get => _editQuantity;
        set => Set(ref _editQuantity, value);
    }

    private IndustryInventoryItemView? _selectedItem;
    public IndustryInventoryItemView? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Set(ref _selectedItem, value))
            {
                EditQuantity = value?.Quantity ?? 1;
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private string _totalText = LanguageService.Get("IndustryInventory_Empty");
    public string TotalText
    {
        get => _totalText;
        set => Set(ref _totalText, value);
    }

    public bool HasWarehouse => SelectedWarehouse is not null;

    // ───────────── 生命周期 ─────────────

    private bool _languageHooked;

    public void Load()
    {
        Characters.Clear();
        foreach (var character in CharacterStore.Characters)
        {
            Characters.Add(new IndustryCharacterImportItem(character));
        }
        ReloadWarehouses(null);

        if (!_languageHooked)
        {
            _languageHooked = true;
            LanguageService.LanguageChanged += (_, _) =>
            {
                // 仓库摘要 / 合计 / 状态文字都是生成时拼好的，切语言后重建
                ReloadWarehouses(SelectedWarehouse?.Id);
                if (SelectedWarehouse is not null)
                {
                    RefreshItems();
                }
            };
        }
    }

    private void ReloadWarehouses(string? selectId)
    {
        var previous = selectId ?? SelectedWarehouse?.Id;
        Warehouses.Clear();
        foreach (var warehouse in IndustryInventoryService.Warehouses.OrderBy(p => p.Name))
        {
            Warehouses.Add(new IndustryWarehouseItemVm
            {
                Id = warehouse.Id,
                Name = warehouse.Name,
                Summary = BuildSummary(warehouse),
            });
        }

        SelectedWarehouse = Warehouses.FirstOrDefault(p => p.Id == previous) ?? Warehouses.FirstOrDefault();
        RefreshItems();
    }

    private static string BuildSummary(IndustryWarehouse warehouse)
    {
        long quantity = warehouse.Items?.Values.Sum() ?? 0;
        int kinds = warehouse.Items?.Count(p => p.Value > 0) ?? 0;
        return LanguageService.Format("IndustryInventory_Summary", kinds, quantity);
    }

    private IndustryWarehouse? CurrentWarehouse()
        => IndustryInventoryService.Find(SelectedWarehouse?.Id);

    private void RefreshItems()
    {
        Items.Clear();
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            TotalText = LanguageService.Format("IndustryInventory_Summary", 0, 0);
            OnPropertyChanged(nameof(HasWarehouse));
            return;
        }

        var ids = warehouse.Items.Where(p => p.Value > 0).Select(p => p.Key).ToList();
        var byId = new Dictionary<int, InvType>();
        if (ids.Count > 0)
        {
            try
            {
                foreach (var group in InvTypeService.QueryTypes(ids).GroupBy(p => p.TypeID))
                {
                    byId[group.Key] = group.First();
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        foreach (var pair in warehouse.Items.Where(p => p.Value > 0).OrderByDescending(p => p.Value))
        {
            byId.TryGetValue(pair.Key, out var type);
            double unit = type?.PackagedVolume > 0 ? type.PackagedVolume : type?.Volume ?? 0;
            Items.Add(new IndustryInventoryItemView
            {
                TypeId = pair.Key,
                Name = type?.TypeName ?? pair.Key.ToString(),
                Quantity = pair.Value,
                Volume = unit * pair.Value,
            });
        }

        long totalQuantity = Items.Sum(p => p.Quantity);
        TotalText = LanguageService.Format("IndustryInventory_Summary", Items.Count, totalQuantity);
        if (SelectedWarehouse is not null)
        {
            SelectedWarehouse.Summary = LanguageService.Format("IndustryInventory_Summary", Items.Count, totalQuantity);
        }
        OnPropertyChanged(nameof(HasWarehouse));
    }

    // ───────────── 仓库动作 ─────────────

    public void CreateWarehouse()
    {
        var warehouse = IndustryInventoryService.Create(NewWarehouseName);
        ReloadWarehouses(warehouse.Id);
        PageNotifyService.Success(LanguageService.Format("IndustryInventory_Created", warehouse.Name));
    }

    public void RenameWarehouse()
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectWarehouse"));
            return;
        }
        if (string.IsNullOrWhiteSpace(NewWarehouseName))
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_EnterName"));
            return;
        }

        IndustryInventoryService.Rename(warehouse, NewWarehouseName);
        ReloadWarehouses(warehouse.Id);
    }

    public void DeleteWarehouse()
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectWarehouse"));
            return;
        }

        IndustryInventoryService.Delete(warehouse);
        ReloadWarehouses(null);
        PageNotifyService.Info(LanguageService.Format("IndustryInventory_Deleted", warehouse.Name));
    }

    // ───────────── 物品动作 ─────────────

    public async Task SearchAsync()
    {
        var text = SearchText?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var results = await Task.Run(() => InvTypeService.Search(text));
            SearchResults.Clear();
            if (results is not null)
            {
                foreach (var item in results.Where(p => p.Type == DataBaseItemType.InvType).Take(100))
                {
                    SearchResults.Add(item);
                }
            }

            if (SearchResults.Count == 0)
            {
                PageNotifyService.Info(LanguageService.Get("IndustryInventory_NoMatch"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Get("IndustryInventory_SearchFailed"));
        }
    }

    public void AddItem()
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectOrCreateWarehouse"));
            return;
        }
        if (SelectedSearchResult is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectItem"));
            return;
        }

        long quantity = Math.Max(1, (long)Math.Round(AddQuantity));
        warehouse.Items[SelectedSearchResult.ID] = warehouse.Items.TryGetValue(SelectedSearchResult.ID, out var existing)
            ? existing + quantity
            : quantity;
        warehouse.Updated = DateTime.Now;
        IndustryInventoryService.Save();
        RefreshItems();
    }

    public void RemoveItem()
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null || SelectedItem is null)
        {
            return;
        }

        warehouse.Items.Remove(SelectedItem.TypeId);
        warehouse.Updated = DateTime.Now;
        IndustryInventoryService.Save();
        RefreshItems();
    }

    public void SetItemQuantity()
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null || SelectedItem is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectRow"));
            return;
        }

        long quantity = Math.Max(0, (long)Math.Round(EditQuantity));
        if (quantity <= 0)
        {
            warehouse.Items.Remove(SelectedItem.TypeId);
        }
        else
        {
            warehouse.Items[SelectedItem.TypeId] = quantity;
        }

        warehouse.Updated = DateTime.Now;
        IndustryInventoryService.Save();
        RefreshItems();
    }

    // ───────────── 角色导入 ─────────────

    public async Task ImportFromCharactersAsync(bool replace)
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectOrCreateWarehouse"));
            return;
        }

        var selected = Characters.Where(p => p.IsSelected).Select(p => p.Character).ToList();
        if (selected.Count == 0)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectCharacters"));
            return;
        }

        IsBusy = true;
        StatusText = LanguageService.Get("IndustryInventory_ReadingAssets");
        try
        {
            var merged = new Dictionary<int, long>();
            foreach (var character in selected)
            {
                var context = new CharacterContext(character);
                var assets = await CharacterIndustryDataService.GetAssetQuantitiesAsync(context, forceRefresh: true);
                if (assets is null) continue;
                foreach (var pair in assets)
                {
                    merged[pair.Key] = merged.TryGetValue(pair.Key, out var existing) ? existing + pair.Value : pair.Value;
                }
            }

            if (replace)
            {
                warehouse.Items.Clear();
            }
            foreach (var pair in merged)
            {
                warehouse.Items[pair.Key] = warehouse.Items.TryGetValue(pair.Key, out var existing)
                    ? existing + pair.Value
                    : pair.Value;
            }
            warehouse.Updated = DateTime.Now;
            IndustryInventoryService.Save();
            RefreshItems();
            PageNotifyService.Success(LanguageService.Format("IndustryInventory_Imported",
                merged.Count,
                LanguageService.Get(replace ? "IndustryInventory_ReplaceMode" : "IndustryInventory_MergeMode")));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Format("IndustryInventory_AssetImportFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
            StatusText = string.Empty;
        }
    }

    // ───────────── CSV ─────────────

    /// <summary>导入 CSV：列为「TypeId/名称, 数量[, 名称]」，容忍逗号/制表符/分号分隔；表头自动跳过。</summary>
    public void ImportCsv(string path, bool replace)
    {
        var warehouse = CurrentWarehouse();
        if (warehouse is null)
        {
            PageNotifyService.Warning(LanguageService.Get("IndustryInventory_SelectOrCreateWarehouse"));
            return;
        }

        try
        {
            int imported = 0;
            int skipped = 0;
            if (replace)
            {
                warehouse.Items.Clear();
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var parts = line.Split(new[] { ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    skipped++;
                    continue;
                }

                var key = parts[0].Trim().Trim('"');
                var quantityText = parts[1].Trim().Trim('"').Replace(",", string.Empty);
                if (!long.TryParse(quantityText, out var quantity) || quantity <= 0)
                {
                    // 表头行（TypeId,Quantity,...）
                    if (key.Equals("TypeId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    skipped++;
                    continue;
                }

                int? typeId = null;
                if (int.TryParse(key, out var id) && id > 0)
                {
                    typeId = id;
                }
                else
                {
                    var exact = InvTypeService.QueryByName(key, false);
                    if (exact is { Count: > 0 })
                    {
                        typeId = exact[0].TypeID;
                    }
                    else
                    {
                        var fuzzy = InvTypeService.QueryByName(key, true);
                        if (fuzzy is { Count: 1 })
                        {
                            typeId = fuzzy[0].TypeID;
                        }
                    }
                }

                if (!typeId.HasValue)
                {
                    skipped++;
                    continue;
                }

                warehouse.Items[typeId.Value] = warehouse.Items.TryGetValue(typeId.Value, out var existing)
                    ? existing + quantity
                    : quantity;
                imported++;
            }

            warehouse.Updated = DateTime.Now;
            IndustryInventoryService.Save();
            RefreshItems();
            PageNotifyService.Success(LanguageService.Format("IndustryInventory_CsvImported", imported, skipped));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Format("IndustryInventory_CsvImportFailed", ex.Message));
        }
    }

    public void ExportCsv(string path)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("TypeId,Quantity,Name");
            foreach (var item in Items)
            {
                builder.Append(item.TypeId).Append(',')
                    .Append(item.Quantity).Append(',')
                    .Append(Csv(item.Name)).AppendLine();
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
            PageNotifyService.Success(LanguageService.Get("IndustryInventory_Exported"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(LanguageService.Format("IndustryInventory_CsvExportFailed", ex.Message));
        }
    }

    private static string Csv(string value)
        => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    // ───────────── INPC ─────────────

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
