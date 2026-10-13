using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 市场槽位：允许"已添加但尚未选择"的占位行（<see cref="Location"/> 为 null）。
/// 之所以要包装一层，是因为 <c>MarketLocationSelectorView.SelectedItem</c> 是 TwoWay 绑定——
/// 若直接往集合里放 null 元素，选中结果无处写回（无法给 null 元素赋值属性）。
/// </summary>
public sealed class CompareMarketSlot : INotifyPropertyChanged
{
    private MarketLocation? _location;

    public MarketLocation? Location
    {
        get => _location;
        set
        {
            if (EqualityComparer<MarketLocation?>.Default.Equals(_location, value))
            {
                return;
            }

            _location = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Location)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 比价页 VM。三条关键约定：
/// <list type="number">
///   <item><b>编辑清单与结果是两套数据</b>：左侧 <see cref="SellItems"/> / <see cref="BuyItems"/> 是事实来源，
///     右侧 <see cref="ResultRows"/> 与结果 Tab 只在点「开始计算」时更新（用户要求的交互：设置好再算）。</item>
///   <item><b>智能复用</b>：市场 / 物品 / 价格口径都没变（仅数量变化且口径与数量无关）时，用已有行情纯内存重算，
///     不必重新联网取价——410 件物品重新取价可能要等几分钟。</item>
///   <item><b>等待态页面私有</b>（§9 第 39 条）：用 <see cref="IsBusy"/> + <see cref="BusyText"/> 驱动页内遮罩，
///     不使用全局 <c>WaitingOverlay</c>；完成 / 失败 / 取消仍走右下角非阻塞通知。</item>
/// </list>
/// </summary>
public sealed class CompareMarketPageViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _cancellationTokenSource;
    private List<CompareMarketFailure> _failures = [];
    private readonly List<string> _unresolvedNames = [];

    /// <summary>是否已算出结果（决定"重算"与"过期提示"是否有意义）。</summary>
    private bool _hasResult;

    /// <summary>最近一次取数所用的参数指纹（用于判断能否纯内存重算）。</summary>
    private string? _lastFetchKey;

    /// <summary>"添加物品 / 粘贴导入"当前要加进哪一组（由打开弹层的按钮决定）。</summary>
    private bool _addTargetSell = true;

    /// <summary>批量改动（初始化 / 导入 / 互换）期间抑制逐项副作用。</summary>
    private bool _suppressSideEffects;

    // ==================================================================
    //  设置
    // ==================================================================

    public CompareMarketSetting Setting { get; private set; } = new();

    /// <summary>市场数量或结果变化 → 结果区需要重建列（列头含市场名与本地化的"卖 / 买"）。</summary>
    public event Action? ColumnsInvalidated;

    // ==================================================================
    //  市场
    // ==================================================================

    public ObservableCollection<CompareMarketSlot> MarketSlots { get; } = [];

    /// <summary>有效市场（已选择位置的槽位）。</summary>
    public List<MarketLocation> Markets
        => MarketSlots.Where(p => p.Location is not null).Select(p => p.Location!).ToList();

    public int MarketCount => MarketSlots.Count(p => p.Location is not null);

    public bool IsSingleMarketHint => MarketCount == 1 && (SellCount + BuyCount) > 0;

    public int MaxMarkets => CompareMarketSetting.MaxMarkets;

    // ==================================================================
    //  编辑清单（左侧，事实来源）
    // ==================================================================

    public ObservableCollection<CompareMarketItemEdit> SellItems { get; } = [];

    public ObservableCollection<CompareMarketItemEdit> BuyItems { get; } = [];

    public int SellCount => SellItems.Count;

    public int BuyCount => BuyItems.Count;

    public bool HasSellItems => SellItems.Count > 0;

    public bool HasBuyItems => BuyItems.Count > 0;

    public string SellResultHeader => $"{FindString("CompareMarketPage_SellGroup")} ({SellCount})";

    public string BuyResultHeader => $"{FindString("CompareMarketPage_BuyGroup")} ({BuyCount})";

    // ==================================================================
    //  添加物品 / 粘贴导入（两个共用的弹出层）
    // ==================================================================

    private List<int> _treeSelected = [];

    /// <summary>
    /// 物品树勾选。控件会**就地增删**这个列表（所以只在点"加入清单"时读取），而"清空"必须走本属性（带通知）：
    /// 直接写字段的话，弹层里的 TwoWay 绑定还指着旧实例，下一次打开就会"勾了物品却读不到"（§9 第 86 条）。
    /// </summary>
    public List<int> TreeSelected
    {
        get => _treeSelected;
        set => Set(ref _treeSelected, value);
    }

    /// <summary>弹出层标题："添加到：卖出物品"。</summary>
    public string AddTargetTitle
        => string.Format(FindString("CompareMarketPage_AddTarget"), GroupName(_addTargetSell));

    /// <summary>"加入卖出清单" / "加入买入清单"。</summary>
    public string AddToListText
        => _addTargetSell ? FindString("CompareMarketPage_AddToSellList") : FindString("CompareMarketPage_AddToBuyList");

    /// <summary>粘贴弹层标题："粘贴导入到：卖出物品"。</summary>
    public string PasteTargetTitle
        => string.Format(FindString("CompareMarketPage_PasteTarget"), GroupName(_addTargetSell));

    private string _pasteText = string.Empty;

    public string PasteText
    {
        get => _pasteText;
        set => Set(ref _pasteText, value);
    }

    /// <summary>打开"添加物品 / 粘贴导入"弹层前调用：决定这次加进哪一组。</summary>
    public void BeginAdd(bool sell)
    {
        if (_addTargetSell == sell)
        {
            return;
        }

        _addTargetSell = sell;
        OnPropertyChanged(nameof(AddTargetTitle));
        OnPropertyChanged(nameof(AddToListText));
        OnPropertyChanged(nameof(PasteTargetTitle));
    }

    public bool HasUnresolved => _unresolvedNames.Count > 0;

    public string UnresolvedText
        => _unresolvedNames.Count == 0
            ? string.Empty
            : string.Format(FindString("CompareMarketPage_UnresolvedNames"), string.Join("、", _unresolvedNames));

    // ==================================================================
    //  结果（右侧，只在"开始计算"后更新）
    // ==================================================================

    public ObservableCollection<CompareMarketRow> ResultRows { get; } = [];

    /// <summary>卖出结果 Tab（"全部" + 各市场）。</summary>
    public ObservableCollection<CompareResultTab> SellResultTabs { get; } = [];

    /// <summary>买入结果 Tab（"全部" + 各市场）。</summary>
    public ObservableCollection<CompareResultTab> BuyResultTabs { get; } = [];

    /// <summary>市场名（结果区的矩阵列头要用）。</summary>
    public ObservableCollection<string> MarketNames { get; } = [];

    public bool HasResult => _hasResult && ResultRows.Count > 0;

    private bool _isResultStale;

    /// <summary>清单或设置已修改、结果尚未重算（界面给一条提示）。</summary>
    public bool IsResultStale
    {
        get => _isResultStale;
        private set => Set(ref _isResultStale, value);
    }

    // ==================================================================
    //  汇总
    // ==================================================================

    public double TotalIncome { get; private set; }

    public double TotalExpense { get; private set; }

    public double NetAmount => TotalIncome - TotalExpense;

    public double TotalVolume { get; private set; }

    public int NoQuoteCount { get; private set; }

    public ConcentrationResult? SellConcentration { get; private set; }

    public ConcentrationResult? BuyConcentration { get; private set; }

    /// <summary>卖出组"分散 vs 集中在最优单一市场"的差额（用户要的数值型结论，不用自然语言）。</summary>
    public double SellGain => SellConcentration?.Gain ?? 0;

    /// <summary>卖出组差额百分比。</summary>
    public double? SellGainPercent => SellConcentration?.HasResult == true ? SellConcentration.GainPercent : null;

    /// <summary>卖出组是否有可展示的差额（无结果时整行隐藏）。</summary>
    public bool HasSellGain => SellConcentration?.HasResult == true;

    /// <summary>卖出组差额行标签："分散 vs 单一市场（多迪谢）"。</summary>
    public string SellGainLabel => BuildGainLabel(SellConcentration);

    /// <summary>买入组"分散 vs 集中在最优单一市场"的差额。</summary>
    public double BuyGain => BuyConcentration?.Gain ?? 0;

    /// <summary>买入组差额百分比。</summary>
    public double? BuyGainPercent => BuyConcentration?.HasResult == true ? BuyConcentration.GainPercent : null;

    /// <summary>买入组是否有可展示的差额。</summary>
    public bool HasBuyGain => BuyConcentration?.HasResult == true;

    /// <summary>买入组差额行标签。</summary>
    public string BuyGainLabel => BuildGainLabel(BuyConcentration);

    public bool HasFailures => _failures.Count > 0;

    public string FailureText
        => _failures.Count == 0
            ? string.Empty
            : string.Format(FindString("CompareMarketPage_FailedMarkets"), _failures.Count, string.Join("、", _failures.Select(p => p.MarketName)));

    /// <summary>警告条整体可见性（任一条需要显示）。</summary>
    public bool HasWarnings
        => HasFailures || HasUnresolved || IsResultStale || IsSingleMarketHint;

    private bool _warningDismissed;

    /// <summary>警告条是否显示（用户可关闭；重新计算后自动恢复）。</summary>
    public bool IsWarningVisible => HasWarnings && !_warningDismissed;

    /// <summary>关闭警告条（失败市场 / 无报价 / 未识别 / 结果过期 / 单市场提示）。</summary>
    public void DismissWarning()
    {
        _warningDismissed = true;
        OnPropertyChanged(nameof(IsWarningVisible));
    }

    // ==================================================================
    //  状态
    // ==================================================================

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    private string _busyText = string.Empty;

    /// <summary>页内等待遮罩的文案（随取数进度刷新）。</summary>
    public string BusyText
    {
        get => _busyText;
        private set => Set(ref _busyText, value);
    }

    // ==================================================================
    //  口径与成交方式
    // ==================================================================

    public int SellPriceBasisIndex
    {
        get => Setting.SellPriceType switch
        {
            ScalperSetting.PriceType.SellTop => 1,
            ScalperSetting.PriceType.SellAvailable => 2,
            _ => 0,
        };
        set
        {
            Setting.SellPriceType = value switch
            {
                1 => ScalperSetting.PriceType.SellTop,
                2 => ScalperSetting.PriceType.SellAvailable,
                _ => ScalperSetting.PriceType.SellTop5,
            };
            OnPropertyChanged();
            OnSettingChanged();
        }
    }

    public int BuyPriceBasisIndex
    {
        get => Setting.BuyPriceType switch
        {
            ScalperSetting.PriceType.BuyTop => 1,
            ScalperSetting.PriceType.BuyAvailable => 2,
            _ => 0,
        };
        set
        {
            Setting.BuyPriceType = value switch
            {
                1 => ScalperSetting.PriceType.BuyTop,
                2 => ScalperSetting.PriceType.BuyAvailable,
                _ => ScalperSetting.PriceType.BuyTop5,
            };
            OnPropertyChanged();
            OnSettingChanged();
        }
    }

    /// <summary>卖出方式：0 = 挂卖单、1 = 直接卖给买单。</summary>
    public int SellModeIndex
    {
        get => Setting.SellMode == CompareTradeMode.MakeOrder ? 0 : 1;
        set
        {
            Setting.SellMode = value == 1 ? CompareTradeMode.TakeOrder : CompareTradeMode.MakeOrder;
            OnPropertyChanged();
            OnSettingChanged();
        }
    }

    /// <summary>买入方式：0 = 从卖单买入、1 = 挂买单。</summary>
    public int BuyModeIndex
    {
        get => Setting.BuyMode == CompareTradeMode.TakeOrder ? 0 : 1;
        set
        {
            Setting.BuyMode = value == 1 ? CompareTradeMode.MakeOrder : CompareTradeMode.TakeOrder;
            OnPropertyChanged();
            OnSettingChanged();
        }
    }

    public double SellTaxPercent
    {
        get => Setting.SellTaxPercent;
        set
        {
            if (Math.Abs(Setting.SellTaxPercent - value) < 0.0001)
            {
                return;
            }

            Setting.SellTaxPercent = Math.Clamp(value, 0, 100);
            OnPropertyChanged();
            OnSettingChanged();
        }
    }

    /// <summary>口径 / 成交方式 / 税费变化：保存 + 把结果标记为过期（等用户点「开始计算」）。</summary>
    private void OnSettingChanged()
    {
        SaveSetting();
        if (_hasResult)
        {
            IsResultStale = true;
        }
    }

    // ==================================================================
    //  生命周期
    // ==================================================================

    public void Init()
    {
        // 页面是常驻缓存页（NavigationCacheMode=Required）：只在首次进入时读设置并重建清单，
        // 切走再切回保留已算出的结果。
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Setting = CompareMarketSettingService.Load();

        _suppressSideEffects = true;
        try
        {
            MarketSlots.Clear();
            foreach (var market in Setting.Markets)
            {
                AddSlot(market);
            }

            foreach (var item in Setting.Items)
            {
                var type = Core.Services.DB.InvTypeService.QueryType(item.TypeId);
                var edit = CreateEdit(type, item.TypeId, item.Amount);
                edit.PropertyChanged += OnEditItemChanged;
                (item.Group == CompareItemGroup.Sell ? SellItems : BuyItems).Add(edit);
            }
        }
        finally
        {
            _suppressSideEffects = false;
        }

        NotifyAll();
    }

    private bool _initialized;

    /// <summary>
    /// 显式取消进行中的取数：**只由页内遮罩上的「取消」按钮触发**。
    /// 不要再挂到页面 <c>Unloaded</c> 上——常驻页切走不是关闭，那样会把"切个页面"变成"比价被静默取消"（§9 第 30 条）。
    /// </summary>
    public void Cancel() => _cancellationTokenSource?.Cancel();

    // ==================================================================
    //  市场增删
    // ==================================================================

    public void AddMarket()
    {
        if (MarketCount >= CompareMarketSetting.MaxMarkets)
        {
            PageNotifyService.Warning(string.Format(FindString("CompareMarketPage_MarketLimit"), CompareMarketSetting.MaxMarkets));
            return;
        }

        _suppressSideEffects = true;
        try
        {
            AddSlot(null);
        }
        finally
        {
            _suppressSideEffects = false;
        }

        NotifyMarketChanged();
    }

    public void RemoveMarket(CompareMarketSlot slot)
    {
        slot.PropertyChanged -= OnSlotChanged;
        MarketSlots.Remove(slot);
        NotifyMarketChanged();
        SaveSetting();
    }

    private void AddSlot(MarketLocation? location)
    {
        var slot = new CompareMarketSlot { Location = location };
        slot.PropertyChanged += OnSlotChanged;
        MarketSlots.Add(slot);
    }

    private void OnSlotChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompareMarketSlot.Location) || sender is not CompareMarketSlot slot || _suppressSideEffects)
        {
            return;
        }

        // 同一个市场只允许出现一次（重复选择时清回占位并提示）
        var location = slot.Location;
        if (location is not null
            && MarketSlots.Any(p => !ReferenceEquals(p, slot) && p.Location is { } other && other.Type == location.Type && other.Id == location.Id))
        {
            PageNotifyService.Warning(FindString("CompareMarketPage_MarketDuplicate"));
            _suppressSideEffects = true;
            try
            {
                slot.Location = null;
            }
            finally
            {
                _suppressSideEffects = false;
            }

            return;
        }

        NotifyMarketChanged();
        SaveSetting();
    }

    private void NotifyMarketChanged()
    {
        OnPropertyChanged(nameof(MarketCount));
        OnPropertyChanged(nameof(IsSingleMarketHint));
        if (_hasResult)
        {
            IsResultStale = true;
        }

        ColumnsInvalidated?.Invoke();
    }

    // ==================================================================
    //  清单维护
    // ==================================================================

    private void OnEditItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSideEffects || e.PropertyName != nameof(CompareMarketItemEdit.Amount))
        {
            return;
        }

        if (_hasResult)
        {
            IsResultStale = true;
        }

        SaveSetting();
    }

    /// <summary>把物品树里勾选的物品加入"当前目标组"（由打开弹层的按钮决定），随后清空勾选。</summary>
    public async Task AddSelectedToTargetAsync()
    {
        var ids = TreeSelected.ToList();
        if (ids.Count == 0)
        {
            PageNotifyService.Warning(FindString("CompareMarketPage_EmptyTreeSelection"));
            return;
        }

        var types = await LoadTypesAsync(ids);

        _suppressSideEffects = true;
        try
        {
            foreach (var id in ids)
            {
                types.TryGetValue(id, out var type);
                MergeOrAdd(type, id, 1, _addTargetSell);
            }
        }
        finally
        {
            _suppressSideEffects = false;
        }

        // 清空树勾选（整体替换 List → 触发控件的 ApplySelectedItems）
        TreeSelected = [];
        AfterItemsChanged();
    }

    /// <summary>粘贴导入到"当前目标组"。</summary>
    public async Task ImportPasteAsync()
    {
        if (string.IsNullOrWhiteSpace(PasteText))
        {
            PageNotifyService.Warning(FindString("CompareMarketPage_EmptyPaste"));
            return;
        }

        var lines = AppraisalTextParser.Parse(PasteText);
        if (lines.Count == 0)
        {
            PageNotifyService.Warning(FindString("CompareMarketPage_EmptyPaste"));
            return;
        }

        var names = lines.Select(p => p.Name).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var resolved = await Task.Run(() => MarketTypeNameResolver.Resolve(names));

        var unresolved = new List<string>();
        var resolvedCount = 0;

        _suppressSideEffects = true;
        try
        {
            foreach (var line in lines)
            {
                if (!resolved.TryGetValue(line.Name, out var type) || type is null)
                {
                    unresolved.Add(line.Name);
                    continue;
                }

                MergeOrAdd(type, type.TypeID, line.Amount, _addTargetSell);
                resolvedCount++;
            }
        }
        finally
        {
            _suppressSideEffects = false;
        }

        _unresolvedNames.Clear();
        _unresolvedNames.AddRange(unresolved.Distinct());
        OnPropertyChanged(nameof(HasUnresolved));
        OnPropertyChanged(nameof(UnresolvedText));

        PasteText = string.Empty;
        AfterItemsChanged();

        PageNotifyService.Success(string.Format(FindString("CompareMarketPage_ImportResult"), lines.Count, resolvedCount));
    }

    public void RemoveEditItems(IEnumerable<CompareMarketItemEdit> items)
    {
        _suppressSideEffects = true;
        try
        {
            foreach (var item in items.ToList())
            {
                item.PropertyChanged -= OnEditItemChanged;
                SellItems.Remove(item);
                BuyItems.Remove(item);
            }
        }
        finally
        {
            _suppressSideEffects = false;
        }

        AfterItemsChanged();
    }

    /// <summary>把物品移到指定组（同一物品只允许出现在一组）。</summary>
    public void MoveEditItems(IEnumerable<CompareMarketItemEdit> items, bool toSell)
    {
        _suppressSideEffects = true;
        try
        {
            foreach (var item in items.ToList())
            {
                var target = toSell ? SellItems : BuyItems;
                var source = toSell ? BuyItems : SellItems;
                if (source.Remove(item))
                {
                    target.Add(item);
                }
            }
        }
        finally
        {
            _suppressSideEffects = false;
        }

        AfterItemsChanged();
    }

    /// <summary>清单发生结构性变化后的统一收尾。</summary>
    private void AfterItemsChanged()
    {
        OnPropertyChanged(nameof(SellCount));
        OnPropertyChanged(nameof(BuyCount));
        OnPropertyChanged(nameof(HasSellItems));
        OnPropertyChanged(nameof(HasBuyItems));
        OnPropertyChanged(nameof(SellResultHeader));
        OnPropertyChanged(nameof(BuyResultHeader));
        OnPropertyChanged(nameof(IsSingleMarketHint));

        if (_hasResult)
        {
            IsResultStale = true;
        }

        SaveSetting();
    }

    private void MergeOrAdd(InvType? type, int typeId, double amount, bool sell)
    {
        // 只在**目标组内**合并同物品：两组是彼此独立的清单，**不要**跨组搬动。
        // （实测反馈：原先"加入另一组就把物品从原组搬走"的规则，在"两组导入同一批货"的常见用法下看起来像"清单被清空"。）
        var target = sell ? SellItems : BuyItems;
        var existing = target.FirstOrDefault(p => p.TypeId == typeId);
        if (existing is not null)
        {
            existing.Amount += amount;
            return;
        }

        var edit = CreateEdit(type, typeId, amount);
        edit.PropertyChanged += OnEditItemChanged;
        target.Add(edit);
    }

    private static CompareMarketItemEdit CreateEdit(InvType? type, int typeId, double amount)
        => new()
        {
            TypeId = typeId,
            Name = type?.TypeName ?? typeId.ToString(),
            UnitVolume = type is null ? 0 : type.PackagedVolume > 0 ? type.PackagedVolume : type.Volume,
            Amount = amount <= 0 ? 1 : amount,
        };

    private static async Task<Dictionary<int, InvType>> LoadTypesAsync(IEnumerable<int> ids)
    {
        var result = new Dictionary<int, InvType>();
        try
        {
            var types = await Core.Services.DB.InvTypeService.QueryTypesAsync(ids.Select(p => (long)p).Distinct().ToList());
            foreach (var type in types)
            {
                result[type.TypeID] = type;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    // ==================================================================
    //  开始计算（智能复用已有行情）
    // ==================================================================

    public bool Validate()
    {
        if (MarketCount == 0)
        {
            PageNotifyService.Error(FindString("CompareMarketPage_EmptyMarkets"));
            return false;
        }

        if (SellItems.Count == 0 && BuyItems.Count == 0)
        {
            PageNotifyService.Error(FindString("CompareMarketPage_EmptyList"));
            return false;
        }

        return true;
    }

    public async Task CalculateAsync()
    {
        if (!Validate())
        {
            return;
        }

        SaveSetting();
        IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        var inputs = BuildInputs();
        var fetchKey = BuildFetchKey(inputs);
        var errorBefore = Core.Log.GetErrorCount();
        var infoBefore = Core.Log.GetInfoCount();
        var stopwatch = Stopwatch.StartNew();

        BusyText = FindString("CompareMarketPage_Running");
        try
        {
            CompareMarketResult result;
            if (_hasResult && fetchKey == _lastFetchKey)
            {
                // 市场 / 物品 / 口径都没变：把新数量写回结果行后纯内存重算（不必重新联网取价）
                ApplyAmountsToResult(inputs);
                BusyText = FindString("CompareMarketPage_Recalculating");
                result = await Task.Run(() => CompareMarketService.Recalculate(ResultRows.ToList(), Markets, _failures, Setting), token);
            }
            else
            {
                result = await CompareMarketService.RunAsync(Setting, inputs, token, OnProgress);
                _lastFetchKey = fetchKey;
            }

            if (token.IsCancellationRequested)
            {
                PageNotifyService.Info(FindString("General_Canceld"));
                return;
            }

            ApplyResult(result);
            IsResultStale = false;
            stopwatch.Stop();
            PageNotifyService.Success(string.Format(
                FindString("CompareMarketPage_Done"),
                result.Rows.Count,
                stopwatch.Elapsed.TotalSeconds.ToString("N1"),
                Core.Log.GetErrorCount() - errorBefore,
                Core.Log.GetInfoCount() - infoBefore));
        }
        catch (OperationCanceledException)
        {
            PageNotifyService.Info(FindString("General_Canceld"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private List<CompareMarketInputItem> BuildInputs()
        => SellItems.Select(p => new CompareMarketInputItem { TypeId = p.TypeId, Name = p.Name, Amount = p.Amount, Group = CompareItemGroup.Sell })
            .Concat(BuyItems.Select(p => new CompareMarketInputItem { TypeId = p.TypeId, Name = p.Name, Amount = p.Amount, Group = CompareItemGroup.Buy }))
            .Where(p => p.TypeId > 0 && p.Amount > 0)
            .ToList();

    /// <summary>
    /// 取数参数指纹：市场列表 + 物品集合 + 两侧口径（含"按数量吃单"时的数量）。
    /// 与上次一致 → 用已有行情重算；不一致 → 重新取数。
    /// </summary>
    private string BuildFetchKey(List<CompareMarketInputItem> inputs)
    {
        var marketPart = string.Join(",", Markets.Select(p => $"{p.Type}:{p.Id}"));
        var typePart = string.Join(",", inputs.Select(p => p.TypeId).OrderBy(p => p));
        var basisPart = $"{(int)Setting.SellPriceType},{(int)Setting.BuyPriceType}";
        var amountPart = IsAmountDependentBasis
            ? string.Join(",", inputs.OrderBy(p => p.TypeId).Select(p => $"{p.TypeId}:{p.Amount:0.##}"))
            : string.Empty;
        return $"{marketPart}|{typePart}|{basisPart}|{amountPart}";
    }

    private bool IsAmountDependentBasis
        => CompareMarketSettingService.IsAmountDependentPriceType(Setting.SellPriceType)
            || CompareMarketSettingService.IsAmountDependentPriceType(Setting.BuyPriceType);

    /// <summary>把编辑清单里的最新数量 / 分组写回结果行（纯内存重算的前置步骤）。</summary>
    private void ApplyAmountsToResult(List<CompareMarketInputItem> inputs)
    {
        var map = inputs.ToDictionary(p => p.TypeId);
        foreach (var row in ResultRows)
        {
            if (map.TryGetValue(row.TypeId, out var input))
            {
                row.Amount = input.Amount;
                row.Group = input.Group;
            }
        }
    }

    /// <summary>进度回调（可能来自后台线程 → 封送到 UI 线程）。</summary>
    private void OnProgress(CompareProgress progress)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            UpdateWaiting(progress);
            return;
        }

        dispatcher.BeginInvoke(() => UpdateWaiting(progress));
    }

    private void UpdateWaiting(CompareProgress progress)
        => BusyText = string.Format(
            FindString("CompareMarketPage_Progress"),
            progress.MarketIndex,
            progress.MarketCount,
            progress.MarketName,
            progress.Done,
            progress.Total);

    // ==================================================================
    //  结果应用
    // ==================================================================

    private void ApplyResult(CompareMarketResult result)
    {
        _failures = result.Failures;
        _hasResult = true;

        // 新结果 → 之前手动关掉的警告条重新显示
        _warningDismissed = false;
        OnPropertyChanged(nameof(IsWarningVisible));

        ResultRows.Clear();
        foreach (var row in result.Rows)
        {
            ResultRows.Add(row);
        }

        TotalIncome = result.TotalSellIncome;
        TotalExpense = result.TotalBuyExpense;
        TotalVolume = result.TotalVolume;
        NoQuoteCount = result.NoQuoteCount;
        SellConcentration = result.SellConcentration;
        BuyConcentration = result.BuyConcentration;

        // 市场名（结果区矩阵列头用）
        MarketNames.Clear();
        foreach (var market in result.Markets)
        {
            MarketNames.Add(market.Name ?? string.Empty);
        }

        BuildResultTabs(result);

        OnPropertyChanged(nameof(TotalIncome));
        OnPropertyChanged(nameof(TotalExpense));
        OnPropertyChanged(nameof(NetAmount));
        OnPropertyChanged(nameof(TotalVolume));
        OnPropertyChanged(nameof(NoQuoteCount));
        OnPropertyChanged(nameof(SellConcentration));
        OnPropertyChanged(nameof(BuyConcentration));
        OnPropertyChanged(nameof(HasSellGain));
        OnPropertyChanged(nameof(HasBuyGain));
        OnPropertyChanged(nameof(SellGain));
        OnPropertyChanged(nameof(BuyGain));
        OnPropertyChanged(nameof(SellGainPercent));
        OnPropertyChanged(nameof(BuyGainPercent));
        OnPropertyChanged(nameof(SellGainLabel));
        OnPropertyChanged(nameof(BuyGainLabel));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(FailureText));

        ColumnsInvalidated?.Invoke();
    }

    /// <summary>
    /// 构建结果 Tab：第一个永远是"全部"（完整矩阵），其后是"被某些物品选为最优"的市场（简表）。
    /// </summary>
    private void BuildResultTabs(CompareMarketResult result)
    {
        BuildResultTabs(result, sellSide: true);
        BuildResultTabs(result, sellSide: false);
    }

    private void BuildResultTabs(CompareMarketResult result, bool sellSide)
    {
        var target = sellSide ? SellResultTabs : BuyResultTabs;
        var group = sellSide ? CompareItemGroup.Sell : CompareItemGroup.Buy;
        var rows = result.Rows.Where(p => p.Group == group).ToList();

        target.Clear();
        target.Add(new CompareResultTab
        {
            Title = $"{FindString("CompareMarketPage_All")} ({rows.Count})",
            MarketIndex = -1,
            Rows = rows,
            TotalSubtotal = rows.Sum(p => p.Subtotal),
        });

        for (var i = 0; i < result.Markets.Count; i++)
        {
            var best = rows.Where(p => (sellSide ? p.SellMarketIndex : p.BuyMarketIndex) == i).ToList();
            if (best.Count == 0)
            {
                continue;
            }

            var simple = best
                .Select(p => new CompareResultSimpleRow
                {
                    Name = p.Name,
                    Amount = p.Amount,
                    Volume = p.Volume,
                    UnitPrice = sellSide ? p.SellUnitPrice : p.BuyUnitPrice,
                    Subtotal = (sellSide ? p.SellUnitPrice : p.BuyUnitPrice) * p.Amount,
                    AdvantagePercent = p.AdvantagePercent,
                })
                .OrderByDescending(p => p.Subtotal)
                .ToList();

            target.Add(new CompareResultTab
            {
                Title = $"{result.Markets[i].Name} ({best.Count})",
                MarketIndex = i,
                SimpleRows = simple,
                TotalSubtotal = simple.Sum(p => p.Subtotal),
            });
        }
    }

    // ==================================================================
    //  建议文案（集中度）
    // ==================================================================

    /// <summary>
    /// 差额行标签："分散 vs 单一市场（多迪谢）"；覆盖不全时在括号里附上覆盖数，
    /// 避免"未覆盖部分被静默漏算"（这里是数值型结论，不再有自然语言建议）。
    /// </summary>
    private static string BuildGainLabel(ConcentrationResult? concentration)
    {
        if (concentration?.HasResult != true)
        {
            return string.Empty;
        }

        var market = concentration.CoversAll
            ? concentration.MarketName
            : $"{concentration.MarketName} · {concentration.CoveredCount}/{concentration.TotalCount}";
        return string.Format(FindString("CompareMarketPage_SplitVsSingle"), market);
    }

    // ==================================================================
    //  保存
    // ==================================================================

    public void SaveSetting()
    {
        if (_suppressSideEffects)
        {
            return;
        }

        Setting.Markets = Markets;
        Setting.Items = SellItems
            .Select(p => new CompareMarketItemSetting { TypeId = p.TypeId, Amount = p.Amount, Group = CompareItemGroup.Sell })
            .Concat(BuyItems.Select(p => new CompareMarketItemSetting { TypeId = p.TypeId, Amount = p.Amount, Group = CompareItemGroup.Buy }))
            .ToList();
        CompareMarketSettingService.Save(Setting);
    }

    private static string GroupName(bool sell)
        => sell ? FindString("CompareMarketPage_SellGroup") : FindString("CompareMarketPage_BuyGroup");

    // ==================================================================
    //  INotifyPropertyChanged
    // ==================================================================

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(Setting));
        OnPropertyChanged(nameof(MarketSlots));
        OnPropertyChanged(nameof(MarketCount));
        OnPropertyChanged(nameof(IsSingleMarketHint));
        OnPropertyChanged(nameof(SellItems));
        OnPropertyChanged(nameof(BuyItems));
        OnPropertyChanged(nameof(SellCount));
        OnPropertyChanged(nameof(BuyCount));
        OnPropertyChanged(nameof(HasSellItems));
        OnPropertyChanged(nameof(HasBuyItems));
        OnPropertyChanged(nameof(SellResultHeader));
        OnPropertyChanged(nameof(BuyResultHeader));
        OnPropertyChanged(nameof(PasteText));
        OnPropertyChanged(nameof(AddTargetTitle));
        OnPropertyChanged(nameof(AddToListText));
        OnPropertyChanged(nameof(PasteTargetTitle));
        OnPropertyChanged(nameof(SellPriceBasisIndex));
        OnPropertyChanged(nameof(BuyPriceBasisIndex));
        OnPropertyChanged(nameof(SellModeIndex));
        OnPropertyChanged(nameof(BuyModeIndex));
        OnPropertyChanged(nameof(SellTaxPercent));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(IsResultStale));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasUnresolved));
        OnPropertyChanged(nameof(UnresolvedText));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(FailureText));
        OnPropertyChanged(nameof(HasSellGain));
        OnPropertyChanged(nameof(HasBuyGain));
        OnPropertyChanged(nameof(SellGainLabel));
        OnPropertyChanged(nameof(BuyGainLabel));
        OnPropertyChanged(nameof(IsWarningVisible));
        OnPropertyChanged(nameof(BusyText));
    }

    /// <summary>
    /// 联动通知表：通知左列属性时顺带通知右列（避免逐处调用遗漏）。
    /// </summary>
    private static readonly Dictionary<string, string[]> DependentNotifications = new()
    {
        [nameof(HasFailures)] = [nameof(HasWarnings)],
        [nameof(HasUnresolved)] = [nameof(HasWarnings)],
        [nameof(IsResultStale)] = [nameof(HasWarnings)],
        [nameof(IsSingleMarketHint)] = [nameof(HasWarnings)],
        [nameof(SellCount)] = [nameof(SellResultHeader)],
        [nameof(BuyCount)] = [nameof(BuyResultHeader)],
        [nameof(HasWarnings)] = [nameof(IsWarningVisible)],
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName is not null && DependentNotifications.TryGetValue(propertyName, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(dependent));
            }
        }
    }

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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
