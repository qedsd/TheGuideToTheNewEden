using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
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

/// <summary>BOM 结果表的一行（已按树深度缩进）。</summary>
public sealed class IndustryPlanRow
{
    public string DisplayName { get; set; } = string.Empty;
    public string DecisionText { get; set; } = string.Empty;
    public long Needed { get; set; }
    public long Produced { get; set; }
    public int Runs { get; set; }
    public long ToBuy { get; set; }
    public double JobCost { get; set; }
}

/// <summary>「工业 → 制造计划」页：搜索目标、设置参数、调用 Core 求解器并展示 BOM。</summary>
public sealed class IndustryPlanPageViewModel : INotifyPropertyChanged
{
    private const int MaxRows = 4000;

    private readonly IndustrySetting _setting;
    private IndustryBuildPlan? _lastPlan;

    public IndustryPlanPageViewModel()
    {
        _setting = IndustrySettingService.Load();
        _meLevel = _setting.DefaultME;
        _teLevel = _setting.DefaultTE;
        _location = _setting.Location;
        _facilityTaxPercent = _setting.FacilityTaxPercent;
        _spaceModifier = _setting.SpaceModifier;
        _rigMeBonusPercent = _setting.RigMeBonusPercent;
        _rigTeBonusPercent = _setting.RigTeBonusPercent;
        _useAssets = _setting.UseAssets;
        _useInProgressJobs = _setting.UseInProgressJobs;
        _useOwnedBlueprints = _setting.UseOwnedBlueprints;
        _selectedCharacter = CharacterStore.Characters.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DataBaseSearchItem> SearchResults { get; } = new();

    public ObservableCollection<AuthorizedCharacterData> Characters => CharacterStore.Characters;

    public ObservableCollection<IndustryPlanRow> Rows { get; } = new();

    // ───────── 目标与参数 ─────────

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
        set
        {
            if (Set(ref _selectedSearchResult, value))
            {
                OnPropertyChanged(nameof(TargetName));
            }
        }
    }

    public string TargetName => _selectedSearchResult?.Name ?? "—";

    private AuthorizedCharacterData? _selectedCharacter;
    public AuthorizedCharacterData? SelectedCharacter
    {
        get => _selectedCharacter;
        set => Set(ref _selectedCharacter, value);
    }

    private double _quantity = 1;
    public double Quantity
    {
        get => _quantity;
        set => Set(ref _quantity, value);
    }

    private double _meLevel;
    public double MeLevel
    {
        get => _meLevel;
        set => Set(ref _meLevel, value);
    }

    private double _teLevel;
    public double TeLevel
    {
        get => _teLevel;
        set => Set(ref _teLevel, value);
    }

    private MarketLocation? _location;
    public MarketLocation? SelectedLocation
    {
        get => _location;
        set => Set(ref _location, value);
    }

    private double _facilityTaxPercent;
    public double FacilityTaxPercent
    {
        get => _facilityTaxPercent;
        set => Set(ref _facilityTaxPercent, value);
    }

    private double _spaceModifier;
    public double SpaceModifier
    {
        get => _spaceModifier;
        set => Set(ref _spaceModifier, value);
    }

    private double _rigMeBonusPercent;
    public double RigMeBonusPercent
    {
        get => _rigMeBonusPercent;
        set => Set(ref _rigMeBonusPercent, value);
    }

    private double _rigTeBonusPercent;
    public double RigTeBonusPercent
    {
        get => _rigTeBonusPercent;
        set => Set(ref _rigTeBonusPercent, value);
    }

    private bool _useAssets;
    public bool UseAssets
    {
        get => _useAssets;
        set => Set(ref _useAssets, value);
    }

    private bool _useInProgressJobs;
    public bool UseInProgressJobs
    {
        get => _useInProgressJobs;
        set => Set(ref _useInProgressJobs, value);
    }

    private bool _useOwnedBlueprints;
    public bool UseOwnedBlueprints
    {
        get => _useOwnedBlueprints;
        set => Set(ref _useOwnedBlueprints, value);
    }

    // ───────── 汇总 ─────────

    private string _summaryRuns = "0";
    public string SummaryRuns
    {
        get => _summaryRuns;
        private set => Set(ref _summaryRuns, value);
    }

    private string _summaryJobCost = "0";
    public string SummaryJobCost
    {
        get => _summaryJobCost;
        private set => Set(ref _summaryJobCost, value);
    }

    private string _summaryDirectBuy = "—";
    public string SummaryDirectBuy
    {
        get => _summaryDirectBuy;
        private set => Set(ref _summaryDirectBuy, value);
    }

    private string _summarySelfBuild = "—";
    public string SummarySelfBuild
    {
        get => _summarySelfBuild;
        private set => Set(ref _summarySelfBuild, value);
    }

    // ───────── 动作 ─────────

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
                PageNotifyService.Info(Resolve("IndustryPage_NoSearchResult"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_SearchFailed"));
        }
    }

    public async Task CalculateAsync()
    {
        if (SelectedSearchResult is null)
        {
            PageNotifyService.Warning(Resolve("IndustryPage_NoTarget"));
            return;
        }

        PageNotifyService.ShowWaiting(Resolve("IndustryPage_Prepare"));
        try
        {
            SaveSetting();

            var character = SelectedCharacter;
            CharacterContext? context = character is null ? null : new CharacterContext(character);
            var options = await IndustryOptionsFactory.BuildAsync(
                context,
                SelectedLocation?.SolarSystemId ?? 0,
                FacilityTaxPercent / 100.0,
                (int)Math.Round(MeLevel),
                (int)Math.Round(TeLevel),
                SpaceModifier,
                RigMeBonusPercent / 100.0,
                RigTeBonusPercent / 100.0,
                UseAssets,
                UseInProgressJobs,
                UseOwnedBlueprints);

            PageNotifyService.UpdateWaiting(Resolve("IndustryPage_Expanding"));

            int targetId = SelectedSearchResult.ID;
            long quantity = Math.Max(1, (long)Math.Round(Quantity));
            var solver = new IndustrySolver(options);
            var plan = await Task.Run(() => solver.Solve(new[] { new IndustryBuildTarget(targetId, quantity) }));

            FillRows(plan);
            PageNotifyService.Success(string.Format(Resolve("IndustryPage_CalcDone"), plan.Summary.BuildRuns.Values.Sum()));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(string.Format(Resolve("IndustryPage_CalcFailed"), ex.Message));
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    public void CopyPurchaseList()
    {
        if (_lastPlan is null || _lastPlan.Summary.BuyQuantities.Count == 0)
        {
            PageNotifyService.Info(Resolve("IndustryPage_NothingToCopy"));
            return;
        }

        try
        {
            var buy = _lastPlan.Summary.BuyQuantities;
            var names = IndustryDbService.GetTypeInfos(buy.Keys)
                .ToDictionary(p => p.TypeID, p => p.TypeName);

            var builder = new StringBuilder();
            foreach (var pair in buy.OrderByDescending(p => p.Value))
            {
                var name = names.TryGetValue(pair.Key, out var typeName) && !string.IsNullOrEmpty(typeName)
                    ? typeName
                    : pair.Key.ToString();
                builder.AppendLine(name + "\t" + pair.Value);
            }

            Clipboard.SetText(builder.ToString());
            PageNotifyService.Success(Resolve("IndustryPage_Copied"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(Resolve("IndustryPage_CopyFailed"));
        }
    }

    // ───────── 内部 ─────────

    private void FillRows(IndustryBuildPlan plan)
    {
        Rows.Clear();
        int count = 0;
        foreach (var target in plan.Targets)
        {
            Walk(target, 0, ref count);
        }

        _lastPlan = plan;
        SummaryRuns = plan.Summary.BuildRuns.Values.Sum().ToString("N0");
        SummaryJobCost = plan.Summary.TotalJobCost.ToString("N2");
        SummaryDirectBuy = plan.Summary.ComparisonKnown ? plan.Summary.DirectBuyCost.ToString("N2") : "—";
        SummarySelfBuild = plan.Summary.ComparisonKnown ? plan.Summary.SelfBuildCost.ToString("N2") : "—";
    }

    private void Walk(IndustryBuildNode node, int level, ref int count)
    {
        if (count >= MaxRows)
        {
            return;
        }

        count++;
        string decision = node.IsInvention
            ? Resolve("IndustryPage_Decision_Invention")
            : node.Decision == IndustryDecision.Buy
                ? Resolve("IndustryPage_Decision_Buy")
                : Resolve("IndustryPage_Decision_Build");

        Rows.Add(new IndustryPlanRow
        {
            DisplayName = new string('\u00A0', level * 3) + node.TypeName,
            DecisionText = decision,
            Needed = node.QuantityNeeded,
            Produced = node.QuantityProduced,
            Runs = node.Runs,
            ToBuy = node.QuantityToBuy,
            JobCost = node.JobCostKnown ? node.JobCost : 0,
        });

        foreach (var input in node.Inputs)
        {
            Walk(input, level + 1, ref count);
        }
    }

    private void SaveSetting()
    {
        _setting.SolarSystemId = SelectedLocation?.SolarSystemId ?? 0;
        _setting.Location = SelectedLocation;
        _setting.FacilityTaxPercent = FacilityTaxPercent;
        _setting.DefaultME = (int)Math.Round(MeLevel);
        _setting.DefaultTE = (int)Math.Round(TeLevel);
        _setting.SpaceModifier = SpaceModifier;
        _setting.RigMeBonusPercent = RigMeBonusPercent;
        _setting.RigTeBonusPercent = RigTeBonusPercent;
        _setting.UseAssets = UseAssets;
        _setting.UseInProgressJobs = UseInProgressJobs;
        _setting.UseOwnedBlueprints = UseOwnedBlueprints;
        IndustrySettingService.Save(_setting);
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
