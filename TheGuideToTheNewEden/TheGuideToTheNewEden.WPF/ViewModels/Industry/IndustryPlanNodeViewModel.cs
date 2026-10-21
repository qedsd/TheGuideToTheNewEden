using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>BOM 树节点（供 TreeView 卡片模板使用）。</summary>
public sealed class IndustryPlanNodeViewModel : INotifyPropertyChanged
{
    public IndustryPlanNodeViewModel(IndustryBuildNode model)
    {
        Model = model;
        DecisionKey = IsAssetCovered
            ? "stock"
            : IsUpstreamCovered ? "surplus"
            : IsJobCovered ? "jobs"
            : IsMixedCovered ? "mixed"
            : model.IsInvention ? "invention" : model.Decision == IndustryDecision.Buy ? "buy" : "build";
        DecisionText = IsAssetCovered
            ? "库存"
            : IsUpstreamCovered ? "上游剩余"
            : IsJobCovered ? "在制"
            : IsMixedCovered ? "混合覆盖"
            : DecisionKey switch
            {
                "invention" => Resolve("IndustryPage_Decision_Invention", "发明"),
                "buy" => Resolve("IndustryPage_Decision_Buy", "采购"),
                _ => Resolve("IndustryPage_Decision_Build", "制造"),
            };
        _activityText = model.ActivityID switch
        {
            IndustryActivity.Manufacturing => Resolve("IndustryActivity_Manufacturing", "制造"),
            IndustryActivity.Reaction => Resolve("IndustryActivity_Reaction", "反应"),
            IndustryActivity.Invention => Resolve("IndustryActivity_Invention", "发明"),
            IndustryActivity.Copying => Resolve("IndustryActivity_Copying", "复制"),
            IndustryActivity.ResearchMaterialEfficiency => Resolve("IndustryActivity_ResearchMaterial", "材料研究"),
            IndustryActivity.ResearchTimeEfficiency => Resolve("IndustryActivity_ResearchTime", "时间研究"),
            IndustryActivity.ReverseEngineering => Resolve("IndustryActivity_ReverseEngineering", "逆向工程"),
            _ => string.Empty,
        };
    }

    public IndustryBuildNode Model { get; }

    public int TypeId => Model.TypeID;
    public string Name => Model.TypeName;
    public string TargetQuantityText => $"请求 {Needed:N0} · 产出 {Produced:N0}";
    public string TargetCostText => FullBuildCost > 0 ? $"{FullBuildCost:N0} ISK" : "—";
    public string TargetCashText => CashCost > 0 ? $"{CashCost:N0} ISK" : "—";
    public string TargetStockText => Model.QuantityFromStock > 0 ? $"{Model.QuantityFromStock:N0} 库存" : "—";
    public string TargetRevenueText => Revenue > 0 ? $"{Revenue:N0} ISK" : "—";
    public bool HasTargetProfit => Model.RevenueUnitPrice > 0;
    public bool IsProfitPositive => HasTargetProfit && Profit >= 0;
    public string TargetProfitText => HasTargetProfit ? $"{Profit:N0} ISK ({Margin:P0})" : "—";
    public string TargetJobCostText => Model.JobCostKnown ? $"{Model.JobCost:N0} ISK" : "—";
    public string TargetRunsText => Runs > 0 ? $"{Runs:N0} 流程" : "—";
    public long FromAssets => Model.QuantityFromAssets;
    public long FromJobs => Model.QuantityFromJobs;
    public long FromUpstream => Model.QuantityFromHangar;
    public long FromStock => Model.QuantityFromStock;
    public bool IsStockCovered => Model.QuantityNeeded > 0
        && FromStock >= Model.QuantityNeeded
        && ((Model.Decision == IndustryDecision.Build && Model.Runs == 0)
            || (Model.Decision == IndustryDecision.Buy && Model.QuantityToBuy == 0));
    public bool IsAssetCovered => IsStockCovered
        && FromJobs == 0
        && FromUpstream == 0
        && FromAssets >= Model.QuantityNeeded;
    public bool IsUpstreamCovered => IsStockCovered
        && FromAssets == 0
        && FromJobs == 0
        && FromUpstream >= Model.QuantityNeeded;
    public bool IsJobCovered => IsStockCovered
        && FromAssets == 0
        && FromUpstream == 0
        && FromJobs >= Model.QuantityNeeded;
    public bool IsMixedCovered => IsStockCovered
        && !IsAssetCovered
        && !IsUpstreamCovered
        && !IsJobCovered;
    public string DecisionText { get; }
    public string DecisionKey { get; }
    private readonly string _activityText;
    public string ActivityText => IsStockCovered ? string.Empty : _activityText;
    public bool HasActivity => !string.IsNullOrEmpty(ActivityText);
    public bool IsBuild => DecisionKey == "build";
    public bool IsBuy => DecisionKey == "buy";
    public bool IsInvention => DecisionKey == "invention";

    public long Needed => Model.QuantityNeeded;
    public long Produced => Model.QuantityProduced;
    public long ToBuy => Model.QuantityToBuy;
    public int Runs => Model.Runs;
    public int ME => Model.ME;
    public int TE => Model.TE;
    public double JobCost => Model.JobCost;
    public double FullBuildCost => Model.FullBuildCost;
    public double CashCost => Model.BuildCost;
    public double UnitPrice => Model.ReferenceUnitPrice;
    public double Revenue => Model.Revenue;
    public double Profit => Model.Profit;
    public double Margin => Model.Margin;
    public double SellMin => Model.SellMinPrice;
    public double BuyMax => Model.BuyMaxPrice;
    public long SellVolume => Model.SellVolume;
    public long BuyVolume => Model.BuyVolume;
    public double InventionProbability => Model.InventionProbability;
    public int InventionOutputRuns => Model.InventionOutputRuns;
    public int InventionOutputME => Model.InventionOutputME;
    public int InventionOutputTE => Model.InventionOutputTE;
    public string DecryptorName => Model.DecryptorName;
    public bool HasInventionData => Model.IsInvention;

    public string QuantityText => Model.IsInvention
        ? $"BPC 需要 {Needed:N0} 张 · 产出 {Produced:N0} 张 · 采购 {ToBuy:N0}"
        : IsStockCovered
            ? $"需要 {Needed:N0} · {CoverageText} · 采购 0"
            : FromStock > 0
                ? $"需要 {Needed:N0} · {CoverageText} · 产出 {Produced:N0} · 采购 {ToBuy:N0}"
                : $"需要 {Needed:N0} · 产出 {Produced:N0} · 采购 {ToBuy:N0}";
    public bool HasStock => FromStock > 0;
    public string CoverageText
    {
        get
        {
            var parts = new List<string>();
            if (FromAssets > 0) parts.Add($"角色资产 {FromAssets:N0}");
            if (FromJobs > 0) parts.Add($"在制 {FromJobs:N0}");
            if (FromUpstream > 0) parts.Add($"上游剩余 {FromUpstream:N0}");
            return string.Join(" · ", parts);
        }
    }
    public string StockText => HasStock ? $"库存抵扣 {FromStock:N0}（{CoverageText}）" : string.Empty;
    public string FullCostLabel => IsStockCovered ? "库存市值" : "总成本";
    public string FullCostHint => IsStockCovered
        ? "完全由库存覆盖：显示库存材料市值，现金支出为 0。"
        : "材料全量市值（含库存中的材料）+ 任务费用。";
    public string RunsText => Runs <= 0
        ? string.Empty
        : Model.IsInvention ? $"{Runs:N0} 次尝试" : $"{Runs:N0} 流程";
    public string MeTeText => !IsStockCovered && Model.Decision == IndustryDecision.Build ? $"ME {ME} · TE {TE}" : string.Empty;
    public bool HasMeTe => !string.IsNullOrEmpty(MeTeText);
    public string JobTimeText => Model.JobTimeSeconds > 0 ? FormatDuration(Model.JobTimeSeconds) : string.Empty;
    public string JobCostText => Model.JobCostKnown ? Model.JobCost.ToString("N2") : "—";
    public string FullBuildCostText => Model.FullBuildCost > 0 ? Model.FullBuildCost.ToString("N0") : "—";
    public string CashCostText => Model.BuildCost > 0 ? Model.BuildCost.ToString("N0") : "—";
    public string UnitPriceText => Model.ReferenceUnitPrice > 0 ? Model.ReferenceUnitPrice.ToString("N2") : "—";
    public string MarketText => SellMin > 0 || BuyMax > 0
        ? $"卖 {SellMin:N2} / 买 {BuyMax:N2}"
        : "无市场深度";
    public string InventionText => $"成功率 {InventionProbability:P1} · 尝试 {Runs:N0} 次 · BPC 需要 {Model.QuantityNeeded:N0} 张 × {InventionOutputRuns} run · 期望成功 {Runs * InventionProbability:0.##} 张 · ME{InventionOutputME}/TE{InventionOutputTE}";
    public string InventionCostHint => Model.IsInvention && Runs > 0
        ? $"成本按 {Runs:N0} 次尝试计：自动时尝试 = ceil(BPC 需求 / 成功率)；每次尝试消耗数据核心/解密器/T1 蓝图（未拥有时），失败同样消耗；另加发明任务费用。"
        : string.Empty;
    public bool HasInventionWarning => Model.IsInvention
        && Runs > 0
        && Runs * InventionProbability + 0.000001 < Model.QuantityNeeded;
    public string InventionWarning => HasInventionWarning
        ? $"期望成功 {Runs * InventionProbability:0.##} 张 < 需要 {Model.QuantityNeeded:N0} 张，当前尝试次数可能不够。"
        : string.Empty;
    public string DecryptorText => string.IsNullOrEmpty(DecryptorName) ? "无解密器" : DecryptorName;

    public double MaterialCost => Math.Max(0, Model.BuildCost - Model.JobCost);
    public double StockValue => Math.Max(0, Model.FullBuildCost - Model.BuildCost);
    public double CostSharePercent { get; set; }
    public string CostShareText => $"占计划总成本 {CostSharePercent:0.#}%";
    public double MaterialShare => Model.FullBuildCost > 0 ? MaterialCost / Model.FullBuildCost * 100 : 0;
    public double JobShare => Model.FullBuildCost > 0 ? Model.JobCost / Model.FullBuildCost * 100 : 0;
    public double StockShare => Model.FullBuildCost > 0 ? StockValue / Model.FullBuildCost * 100 : 0;

    public ObservableCollection<IndustryPlanNodeViewModel> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;

    public int Depth { get; set; }
    public Thickness IndentMargin { get; set; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string Resolve(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "—";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{span.Minutes}m {span.Seconds}s";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
