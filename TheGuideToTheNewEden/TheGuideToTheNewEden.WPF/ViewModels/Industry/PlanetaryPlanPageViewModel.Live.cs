using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Planetary;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Industry;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed partial class PlanetaryPlanPageViewModel
{
    private CancellationTokenSource? _liveCts;

    public ObservableCollection<PlanetaryLivePlanetItem> LivePlanets { get; } = new();
    public ObservableCollection<PlanetaryLiveCharacterGroup> LiveCharacters { get; } = new();

    private bool _isLiveBusy;
    public bool IsLiveBusy
    {
        get => _isLiveBusy;
        set
        {
            if (!Set(ref _isLiveBusy, value)) return;
            Notify(nameof(IsLiveNotBusy));
            Notify(nameof(IsAnyBusy));
            Notify(nameof(BusyNoticeText));
        }
    }

    public bool IsLiveNotBusy => !IsLiveBusy;

    private string _liveStatusText = "读取殖民地";
    public string LiveStatusText
    {
        get => _liveStatusText;
        set
        {
            if (!Set(ref _liveStatusText, value)) return;
            Notify(nameof(BusyNoticeText));
        }
    }

    private string _liveCharacterSummaryText = "暂无殖民地";
    public string LiveCharacterSummaryText
    {
        get => _liveCharacterSummaryText;
        set => Set(ref _liveCharacterSummaryText, value);
    }

    private PlanetaryLiveCharacterGroup? _selectedLiveCharacter;
    public PlanetaryLiveCharacterGroup? SelectedLiveCharacter
    {
        get => _selectedLiveCharacter;
        set
        {
            if (!Set(ref _selectedLiveCharacter, value)) return;
            SelectedLiveColony = value?.Colonies.FirstOrDefault();
            Notify(nameof(SelectedCharacterSummaryText));
            Notify(nameof(HasSelectedLiveCharacter));
            Notify(nameof(HasNoSelectedLiveCharacter));
        }
    }

    public bool HasSelectedLiveCharacter => SelectedLiveCharacter is not null;
    public bool HasNoSelectedLiveCharacter => SelectedLiveCharacter is null;

    private PlanetaryLivePlanetItem? _selectedLiveColony;
    public PlanetaryLivePlanetItem? SelectedLiveColony
    {
        get => _selectedLiveColony;
        set
        {
            if (!Set(ref _selectedLiveColony, value)) return;
            Notify(nameof(HasSelectedLiveColony));
            Notify(nameof(HasNoSelectedLiveColony));
        }
    }

    public bool HasSelectedLiveColony => SelectedLiveColony is not null;
    public bool HasNoSelectedLiveColony => SelectedLiveColony is null;
    public string SelectedCharacterSummaryText => SelectedLiveCharacter?.Summary ?? "未选择角色";

    private bool _liveScopeMissing;
    public bool LiveScopeMissing
    {
        get => _liveScopeMissing;
        set => Set(ref _liveScopeMissing, value);
    }

    public bool HasLivePlanets => LivePlanets.Count > 0;
    public bool HasNoLivePlanets => LivePlanets.Count == 0;

    public async Task RefreshLiveAsync(bool forceRefresh = false)
    {
        if (_recipes is null)
        {
            await LoadAsync();
        }
        if (_recipes is null) return;

        _liveCts?.Cancel();
        _liveCts = new CancellationTokenSource();
        var token = _liveCts.Token;
        IsLiveBusy = true;
        LiveScopeMissing = false;
        LiveStatusText = "正在读取角色殖民地...";
        try
        {
            var selected = Characters.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0) selected = Characters.ToList();
            if (selected.Count == 0)
            {
                ClearLiveData();
                PageNotifyService.Warning("未选择角色；请先在「计划设置 → 角色与产能」添加或勾选角色");
                return;
            }

            var contexts = selected.Select(p => new CharacterContext(p.Character)).ToList();
            var entries = new List<(CharacterContext Context, PlanetaryColonySummary Colony)>();
            var scopeMissing = new List<string>();
            var errors = new List<string>();
            foreach (var context in contexts)
            {
                token.ThrowIfCancellationRequested();
                var result = await CharacterPlanetaryDataService.GetColoniesAsync(context, forceRefresh);
                if (result.ScopeMissing)
                {
                    scopeMissing.Add(context.Name);
                    continue;
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    errors.Add($"{context.Name}：{result.Error}");
                    continue;
                }
                foreach (var colony in result.Colonies)
                {
                    entries.Add((context, colony));
                }
            }

            LiveScopeMissing = scopeMissing.Count > 0;
            if (entries.Count == 0 && (scopeMissing.Count > 0 || errors.Count > 0))
            {
                ClearLiveData();
                var issues = new List<string>();
                if (scopeMissing.Count > 0)
                {
                    issues.Add("缺少 esi-planets.manage_planets.v1 权限，请重新授权：" + string.Join("、", scopeMissing));
                }
                if (errors.Count > 0)
                {
                    issues.Add(string.Join("；", errors));
                }
                PageNotifyService.Error("殖民地刷新失败：" + string.Join("  ", issues));
                return;
            }

            LiveStatusText = "正在读取殖民地布局...";
            var selectedCharacterId = SelectedLiveCharacter?.CharacterId ?? 0;
            var selectedPlanetId = SelectedLiveColony?.PlanetId ?? 0;
            var semaphore = new SemaphoreSlim(3);
            var tasks = entries.Select(async entry =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    var layout = await CharacterPlanetaryDataService.GetLayoutAsync(
                        entry.Context, entry.Colony.PlanetId, forceRefresh);
                    return (entry.Context, entry.Colony, layout);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();
            var loaded = await Task.WhenAll(tasks);
            token.ThrowIfCancellationRequested();

            LivePlanets.Clear();
            foreach (var item in loaded)
            {
                LivePlanets.Add(BuildLivePlanet(
                    item.Context.CharacterId, item.Context.Name, item.Colony, item.layout));
            }

            RebuildLiveGroups(selectedCharacterId, selectedPlanetId);
            _liveSupplyPerDay.Clear();
            foreach (var factory in LivePlanets.SelectMany(p => p.Factories))
            {
                if (factory.TypeId <= 0 || factory.OutputPerDay <= 0) continue;
                _liveSupplyPerDay[factory.TypeId] = _liveSupplyPerDay.TryGetValue(factory.TypeId, out var existing)
                    ? existing + factory.OutputPerDay
                    : factory.OutputPerDay;
            }

            // 所有失败信息合并成一条右下角通知，页面不显示状态文本。
            var notices = new List<string>();
            if (errors.Count > 0)
            {
                notices.Add("读取失败：" + string.Join("；", errors));
            }
            if (scopeMissing.Count > 0)
            {
                notices.Add("缺少行星权限：" + string.Join("、", scopeMissing));
            }
            if (notices.Count > 0)
            {
                PageNotifyService.Error("殖民地刷新未全部成功：" + string.Join("  ", notices));
            }
            else
            {
                PageNotifyService.Success("殖民地数据已刷新");
            }
        }
        catch (OperationCanceledException)
        {
            PageNotifyService.Info("殖民地刷新已停止");
        }
        catch (Exception ex)
        {
            PageNotifyService.Error("殖民地读取失败：" + ex.Message);
            Core.Log.Error(ex);
        }
        finally
        {
            IsLiveBusy = false;
        }
    }

    public void CancelLive()
    {
        _liveCts?.Cancel();
    }

    private void ClearLiveData()
    {
        LivePlanets.Clear();
        LiveCharacters.Clear();
        SelectedLiveCharacter = null;
        SelectedLiveColony = null;
        LiveCharacterSummaryText = "暂无殖民地";
        Notify(nameof(HasLivePlanets));
        Notify(nameof(HasNoLivePlanets));
    }

    private void RebuildLiveGroups(long selectedCharacterId, long selectedPlanetId)
    {
        LiveCharacters.Clear();
        foreach (var group in LivePlanets
                     .GroupBy(p => p.CharacterId)
                     .OrderBy(p => p.First().CharacterName))
        {
            var colonies = group.OrderBy(p => p.PlanetName).ToList();
            LiveCharacters.Add(new PlanetaryLiveCharacterGroup(
                group.Key, group.First().CharacterName, colonies));
        }

        LiveCharacterSummaryText = LiveCharacters.Count == 0
            ? "暂无殖民地"
            : $"{LiveCharacters.Count} 个角色 · {LivePlanets.Count} 个殖民地";
        Notify(nameof(HasLivePlanets));
        Notify(nameof(HasNoLivePlanets));

        SelectedLiveCharacter = LiveCharacters.FirstOrDefault(p => p.CharacterId == selectedCharacterId)
                                ?? LiveCharacters.FirstOrDefault();
        if (SelectedLiveCharacter is not null && selectedPlanetId > 0)
        {
            SelectedLiveColony = SelectedLiveCharacter.Colonies
                                     .FirstOrDefault(p => p.PlanetId == selectedPlanetId)
                                 ?? SelectedLiveCharacter.Colonies.FirstOrDefault();
        }
    }

    private PlanetaryLivePlanetItem BuildLivePlanet(
        long characterId,
        string characterName,
        PlanetaryColonySummary colony,
        PlanetaryColonyLayoutResult layoutResult)
    {
        var item = new PlanetaryLivePlanetItem
        {
            CharacterId = characterId,
            PlanetId = colony.PlanetId,
            CharacterName = characterName,
            PlanetName = colony.PlanetName,
            PlanetTypeName = PlanetaryNameLocalizer.PlanetType(colony.PlanetTypeName),
            SystemName = colony.SystemName,
            UpgradeLevel = colony.UpgradeLevel,
            NumPins = colony.NumPins,
            LastUpdateText = colony.LastUpdate == default
                ? string.Empty
                : colony.LastUpdate.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
        };
        var layout = layoutResult.Layout;
        if (layout is null)
        {
            item.HasAlert = true;
            item.AlertText = string.IsNullOrEmpty(layoutResult.Error) ? "殖民布局读取失败" : layoutResult.Error;
            return item;
        }

        var produced = new HashSet<int>();
        var consumed = new HashSet<int>();
        var stopped = 0;

        foreach (var pin in layout.Pins.Where(p => p.ExtractorProductTypeId > 0))
        {
            var productName = TypeName(pin.ExtractorProductTypeId);
            var expiry = pin.ExpiryTime;
            var expiryUtc = expiry.HasValue ? ToUtc(expiry.Value) : (DateTime?)null;
            var isStopped = !expiryUtc.HasValue || expiryUtc.Value <= DateTime.UtcNow;
            if (isStopped) stopped++;
            item.Extractors.Add(new PlanetaryLiveExtractorItem
            {
                ProductName = productName,
                QuantityText = pin.QtyPerCycle > 0 ? $"{pin.QtyPerCycle:N0} / 周期" : "无产量",
                CycleText = FormatCycle(pin.CycleTime),
                HeadsText = pin.HeadCount > 0 ? $"{pin.HeadCount} 头" : string.Empty,
                RemainingText = isStopped ? "已停止" : FormatRemaining(expiryUtc.GetValueOrDefault()),
                StatusText = isStopped ? "已停止" : "提取中",
                IsStopped = isStopped,
            });
            produced.Add(pin.ExtractorProductTypeId);
        }

        foreach (var group in layout.Pins
                     .Where(p => p.ExtractorProductTypeId <= 0 && p.FactorySchematicId > 0)
                     .GroupBy(p => p.FactorySchematicId))
        {
            var product = _recipes?.Products.FirstOrDefault(p => p.Recipe?.SchematicId == group.Key);
            if (product?.Recipe is null)
            {
                item.Factories.Add(new PlanetaryLiveFactoryItem
                {
                    ProductName = "未知配方 #" + group.Key,
                    Count = group.Count(),
                });
                continue;
            }

            var recipe = product.Recipe;
            var outputPerDay = recipe.OutputQuantity * 86400.0 / Math.Max(1, recipe.CycleTime) * group.Count();
            var inputText = string.Join("、", recipe.Inputs.Select(p => TypeName(p.TypeID)));
            item.Factories.Add(new PlanetaryLiveFactoryItem
            {
                TypeId = product.TypeId,
                ProductName = PlanetaryNameLocalizer.TypeName(product.TypeId, product.Name),
                Count = group.Count(),
                OutputPerDay = outputPerDay,
                OutputText = $"{outputPerDay:N0}/天",
                CycleText = FormatCycle(recipe.CycleTime),
                InputsText = string.IsNullOrEmpty(inputText) ? "无输入" : inputText,
            });
            produced.Add(product.TypeId);
            foreach (var input in recipe.Inputs)
            {
                consumed.Add(input.TypeID);
            }
        }

        BuildPins(item, layout);
        BuildCapacitySeries(item, layout);

        var storage = layout.Pins
            .SelectMany(p => p.Contents)
            .GroupBy(p => p.TypeId)
            .Select(p => $"{TypeName(p.Key)} ×{p.Sum(x => x.Amount):N0}")
            .ToList();
        item.StorageText = storage.Count == 0 ? "无库存" : string.Join(" · ", storage);
        var imports = consumed.Where(p => !produced.Contains(p)).Select(TypeName).ToList();
        var exports = produced.Where(p => !consumed.Contains(p)).Select(TypeName).ToList();
        item.ImportsText = imports.Count == 0 ? "无" : string.Join("、", imports);
        item.ExportsText = exports.Count == 0 ? "无" : string.Join("、", exports);

        var alerts = new List<string>();
        if (stopped > 0) alerts.Add($"提取器停止 {stopped}");
        if (imports.Count > 0) alerts.Add("需进口");
        item.HasAlert = alerts.Count > 0;
        item.AlertText = string.Join(" · ", alerts);
        return item;
    }

    private void BuildPins(PlanetaryLivePlanetItem item, PlanetaryColonyLayout layout)
    {
        var byId = new Dictionary<long, PlanetaryLivePinItem>();
        foreach (var pin in layout.Pins)
        {
            var role = pin.ExtractorProductTypeId > 0
                ? "提取器"
                : pin.FactorySchematicId > 0
                    ? "工厂"
                    : pin.Contents.Count > 0 ? "仓储" : "设施";
            var typeName = pin.TypeId > 0 ? TypeName(pin.TypeId) : role;
            var mapped = new PlanetaryLivePinItem
            {
                PinId = pin.PinId,
                TypeId = pin.TypeId,
                TypeName = typeName,
                Role = role,
                IsExtractor = pin.ExtractorProductTypeId > 0,
                IsFactory = pin.FactorySchematicId > 0,
                IsStorage = role == "仓储",
            };
            item.Pins.Add(mapped);
            byId[pin.PinId] = mapped;
        }

        // ESI 的 link 只是设施之间的网络连接，不是生产上下级，
        // 因此这里不构造父子树，只按设施类型平铺展示角色节点。
        var connectionCounts = item.Pins.ToDictionary(p => p.PinId, _ => 0);
        foreach (var link in layout.Links)
        {
            if (!byId.ContainsKey(link.SourcePinId) || !byId.ContainsKey(link.DestinationPinId))
            {
                continue;
            }
            connectionCounts[link.SourcePinId]++;
            connectionCounts[link.DestinationPinId]++;
        }

        var facilities = item.Pins
            .Select(p => new PlanetaryFacilityNode
            {
                PinId = p.PinId,
                Role = p.Role,
                TypeName = p.TypeName,
                IsExtractor = p.IsExtractor,
                IsFactory = p.IsFactory,
                IsStorage = p.IsStorage,
                ConnectionCount = connectionCounts[p.PinId],
            })
            .OrderBy(p => RoleOrder(p.Role))
            .ThenBy(p => p.TypeName)
            .ThenBy(p => p.PinId)
            .ToList();
        item.Facilities.AddRange(facilities);
    }

    private static int RoleOrder(string role)
    {
        return role == "设施" ? 0
            : role == "仓储" ? 1
            : role == "提取器" ? 2
            : 3;
    }

    private void BuildCapacitySeries(PlanetaryLivePlanetItem item, PlanetaryColonyLayout layout)
    {
        var extractors = layout.Pins.Where(p => p.ExtractorProductTypeId > 0).ToList();
        if (extractors.Count > 0)
        {
            var now = DateTime.UtcNow;
            var hasActiveExtractor = extractors.Any(p =>
                p.ExpiryTime.HasValue && ToUtc(p.ExpiryTime.Value) > now);
            for (var i = 0; i <= 14; i++)
            {
                var hours = i * 12.0;
                var time = now.AddHours(hours);
                var total = 0.0;
                foreach (var extractor in extractors)
                {
                    if (extractor.QtyPerCycle <= 0 || extractor.CycleTime <= 0) continue;
                    if (!extractor.ExpiryTime.HasValue || ToUtc(extractor.ExpiryTime.Value) <= time) continue;
                    var cycle = (int)Math.Max(1, extractor.CycleTime);
                    var start = extractor.InstallTime.HasValue ? ToUtc(extractor.InstallTime.Value) : now;
                    var elapsed = Math.Max(0, (now - start).TotalSeconds);
                    var cycleIndex = (int)(elapsed / cycle) + (int)(hours * 3600 / cycle);
                    var perCycle = PlanetaryExtractionYield.CycleOutput(
                        (int)extractor.QtyPerCycle, cycle, cycleIndex, true);
                    total += perCycle * 86400.0 / cycle;
                }
                item.ExtractorCapacitySeries.Add(new PlanetaryLiveCapacityPoint
                {
                    Label = i == 0 ? "现在" : $"{hours / 24:0.#}d",
                    Value = total,
                });
            }
            item.ExtractorCapacityHint = hasActiveExtractor ? "运行中" : "已停止";
        }

        if (item.Factories.Count > 0)
        {
            var total = item.Factories.Sum(p => p.OutputPerDay);
            for (var i = 0; i <= 14; i++)
            {
                item.FactoryCapacitySeries.Add(new PlanetaryLiveCapacityPoint
                {
                    Label = i == 0 ? "现在" : $"{(i * 12.0 / 24):0.#}d",
                    Value = total,
                });
            }
            var factoryCount = item.Factories.Sum(p => p.Count);
            item.FactoryCapacityHint =
                $"按当前 {factoryCount} 个工厂与配方循环估算的满产上限；ESI 不提供工厂启停/输入库存，实际可能因缺料停产。";
        }
    }

    private string TypeName(int typeId)
    {
        var product = _recipes?.GetProduct(typeId);
        var fallback = product?.Name ?? IndustryDbService.GetTypeInfo(typeId)?.TypeName ?? ("Type " + typeId);
        return PlanetaryNameLocalizer.TypeName(typeId, fallback);
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static string FormatCycle(long seconds)
    {
        if (seconds <= 0) return string.Empty;
        if (seconds % 3600 == 0) return $"{seconds / 3600} 小时/周期";
        if (seconds % 60 == 0) return $"{seconds / 60} 分/周期";
        return $"{seconds} 秒/周期";
    }

    private static string FormatRemaining(DateTime expiryUtc)
    {
        var span = expiryUtc - DateTime.UtcNow;
        if (span <= TimeSpan.Zero) return "已停止";
        if (span.TotalDays >= 1) return $"剩余 {(int)span.TotalDays} 天 {span.Hours} 小时";
        if (span.TotalHours >= 1) return $"剩余 {span.Hours} 小时 {span.Minutes} 分";
        return $"剩余 {Math.Max(0, span.Minutes)} 分";
    }
}
