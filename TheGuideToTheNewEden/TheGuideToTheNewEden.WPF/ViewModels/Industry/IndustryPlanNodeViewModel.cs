using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

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
    }

    public IndustryBuildNode Model { get; }

    public int TypeId => Model.TypeID;
    public string Name => Model.TypeName;
    public string TargetQuantityText => LanguageService.Format("IndustryChain_TargetQuantity", Needed, Produced);
    public string TargetCostText => FullBuildCost > 0 ? $"{FullBuildCost:N0} ISK" : "—";
    public string TargetCashText => CashCost > 0 ? $"{CashCost:N0} ISK" : "—";
    public string TargetStockText => Model.QuantityFromStock > 0
        ? LanguageService.Format("IndustryChain_StockQuantity", Model.QuantityFromStock)
        : "—";
    public string TargetRevenueText => NetRevenue > 0 ? $"{NetRevenue:N0} ISK" : "—";
    public bool HasTargetProfit => Model.RevenueUnitPrice > 0;
    public bool IsProfitPositive => HasTargetProfit && Profit >= 0;
    public string TargetProfitText => HasTargetProfit ? $"{Profit:N0} ISK ({Margin:P0})" : "—";
    public string TargetJobCostText => Model.JobCostKnown ? $"{Model.JobCost:N0} ISK" : "—";
    public string TargetRunsText => Runs > 0
        ? IsPlanetary
            ? LanguageService.Format("IndustryChain_RunsCycles", Runs)
            : LanguageService.Format("IndustryChain_RunsCount", Runs)
        : "—";
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

    public string DecisionText => IsAssetCovered
        ? LanguageService.Get("IndustryChain_CoverageStock")
        : IsUpstreamCovered ? LanguageService.Get("IndustryChain_CoverageUpstream")
        : IsJobCovered ? LanguageService.Get("IndustryChain_CoverageJob")
        : IsMixedCovered ? LanguageService.Get("IndustryChain_CoverageMixed")
        : DecisionKey switch
        {
            "invention" => LanguageService.Get("IndustryPage_Decision_Invention"),
            "buy" => LanguageService.Get("IndustryPage_Decision_Buy"),
            _ => LanguageService.Get("IndustryPage_Decision_Build"),
        };
    public string DecisionKey { get; }

    /// <summary>语言无关的活动代码：给 DataTrigger 用（不能用本地化文本比较）。</summary>
    public string ActivityKind => Model.ActivityID switch
    {
        IndustryActivity.Manufacturing => "manufacturing",
        IndustryActivity.Reaction => "reaction",
        IndustryActivity.Invention => "invention",
        IndustryActivity.Copying => "copying",
        IndustryActivity.ResearchMaterialEfficiency => "researchMaterial",
        IndustryActivity.ResearchTimeEfficiency => "researchTime",
        IndustryActivity.ReverseEngineering => "reverseEngineering",
        IndustryActivity.Planetary => "planetary",
        _ => string.Empty,
    };

    public string ActivityText => IsStockCovered
        ? string.Empty
        : LanguageService.Get(Model.ActivityID switch
        {
            IndustryActivity.Manufacturing => "IndustryActivity_Manufacturing",
            IndustryActivity.Reaction => "IndustryActivity_Reaction",
            IndustryActivity.Invention => "IndustryActivity_Invention",
            IndustryActivity.Copying => "IndustryActivity_Copying",
            IndustryActivity.ResearchMaterialEfficiency => "IndustryActivity_ResearchMaterial",
            IndustryActivity.ResearchTimeEfficiency => "IndustryActivity_ResearchTime",
            IndustryActivity.ReverseEngineering => "IndustryActivity_ReverseEngineering",
            IndustryActivity.Planetary => "IndustryChain_ActivityPlanetary",
            _ => "IndustryActivity_Manufacturing",
        });
    public bool HasActivity => !string.IsNullOrEmpty(ActivityText);
    public bool IsBuild => DecisionKey == "build";
    public bool IsBuy => DecisionKey == "buy";
    public bool IsInvention => DecisionKey == "invention";
    public bool IsPlanetary => Model.ActivityID == IndustryActivity.Planetary;

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
    public double NetRevenue => Model.NetRevenue;
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
    public string DecryptorName
    {
        get
        {
            if (Model.DecryptorTypeID > 0
                && TheGuideToTheNewEden.Core.Config.NeedLocalization
                && DBService.LocalDbReady)
            {
                try
                {
                    var local = LocalDbService.TranInvType(Model.DecryptorTypeID);
                    if (!string.IsNullOrWhiteSpace(local?.TypeName))
                    {
                        return local.TypeName;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
            return Model.DecryptorName;
        }
    }
    public bool HasInventionData => Model.IsInvention;

    public string QuantityText => Model.IsInvention
        ? LanguageService.Format("IndustryChain_BpcNeed", Needed, Produced, ToBuy)
        : IsStockCovered
            ? LanguageService.Format("IndustryChain_NeedCoveredNoBuy", Needed, CoverageText)
            : FromStock > 0
                ? LanguageService.Format("IndustryChain_NeedCoveredProducedBuy", Needed, CoverageText, Produced, ToBuy)
                : LanguageService.Format("IndustryChain_NeedProducedBuy", Needed, Produced, ToBuy);
    public bool HasStock => FromStock > 0;
    public string CoverageText
    {
        get
        {
            var parts = new List<string>();
            if (FromAssets > 0) parts.Add(LanguageService.Format("IndustryChain_FromAssets", FromAssets));
            if (FromJobs > 0) parts.Add(LanguageService.Format("IndustryChain_FromJobs", FromJobs));
            if (FromUpstream > 0) parts.Add(LanguageService.Format("IndustryChain_FromUpstream", FromUpstream));
            return string.Join(" · ", parts);
        }
    }
    public string StockText => HasStock
        ? LanguageService.Format("IndustryChain_StockCovered", FromStock, CoverageText)
        : string.Empty;
    public string FullCostLabel => IsStockCovered
        ? LanguageService.Get("IndustryChain_StockValueLabel")
        : LanguageService.Get("IndustryPage_TotalCost");
    public string FullCostHint => IsStockCovered
        ? LanguageService.Get("IndustryChain_StockCoveredTip")
        : LanguageService.Get("IndustryPage_TotalCostTipLong");
    public string RunsText => Runs <= 0
        ? string.Empty
        : Model.IsInvention
            ? LanguageService.Format("IndustryChain_AttemptsCount", Runs)
            : IsPlanetary
                ? LanguageService.Format("IndustryChain_RunsCycles", Runs)
                : LanguageService.Format("IndustryChain_RunsCount", Runs);
    public string BlueprintKindText => Model.HasOwnedBpo
        ? "BPO"
        : Model.BpcRunsAvailable > 0
            ? $"BPC ×{Model.BpcCount:N0}"
            : Model.IsInvention
                ? LanguageService.Get("IndustryChain_BlueprintInvention")
                : LanguageService.Get("IndustryChain_NoOwnBlueprint");
    public string BlueprintStockText => Model.HasOwnedBpo
        ? LanguageService.Get("IndustryChain_BpoUnlimited")
        : Model.BpcRunsAvailable <= 0
            ? string.Empty
            : Model.BpcRunsShortage > 0
                ? LanguageService.Format("IndustryChain_BpcRemaining", Model.BpcRunsRemaining, Model.BpcRunsUsed, Model.BpcRunsShortage)
                : LanguageService.Format("IndustryChain_BpcRemainingShort", Model.BpcRunsRemaining, Model.BpcRunsUsed);
    public bool HasBlueprintStockText => !string.IsNullOrEmpty(BlueprintStockText);
    public string MeTeText => !IsStockCovered && !IsPlanetary && Model.Decision == IndustryDecision.Build ? $"ME {ME} · TE {TE}" : string.Empty;
    public bool HasMeTe => !string.IsNullOrEmpty(MeTeText);
    public string JobTimeText => Model.JobTimeSeconds > 0 ? FormatDuration(Model.JobTimeSeconds) : string.Empty;
    public string JobCostText => Model.JobCostKnown ? Model.JobCost.ToString("N2") : "—";
    public string FullBuildCostText => Model.FullBuildCost > 0 ? Model.FullBuildCost.ToString("N0") : "—";
    public string CashCostText => Model.BuildCost > 0 ? Model.BuildCost.ToString("N0") : "—";
    public string UnitPriceText => Model.ReferenceUnitPrice > 0 ? Model.ReferenceUnitPrice.ToString("N2") : "—";
    public string ReferencePriceText => LanguageService.Format("IndustryPage_ReferenceUnitPrice", UnitPriceText);
    public string MarketText => SellMin > 0 || BuyMax > 0
        ? LanguageService.Format("IndustryChain_SellBuy", SellMin, BuyMax)
        : LanguageService.Get("IndustryChain_NoDetailedOrders");
    public string MarketVolumeText => Model.MarketVolumeLimited
        ? LanguageService.Format("IndustryChain_UnfilledVolume", Model.UnfilledQuantity)
        : string.Empty;
    public bool HasMarketVolumeWarning => Model.MarketVolumeLimited;
    public string MarketLevelsText
    {
        get
        {
            var sells = Model.SellLevels?.Take(3).Select(p => $"{p.Price:N2}×{p.Volume:N0}") ?? Enumerable.Empty<string>();
            var buys = Model.BuyLevels?.Take(3).Select(p => $"{p.Price:N2}×{p.Volume:N0}") ?? Enumerable.Empty<string>();
            var sellText = string.Join(" / ", sells);
            var buyText = string.Join(" / ", buys);
            if (string.IsNullOrEmpty(sellText) && string.IsNullOrEmpty(buyText)) return string.Empty;
            return LanguageService.Format("IndustryChain_OrderBook", sellText, buyText);
        }
    }
    public string InventionText => LanguageService.Format("IndustryChain_InventionText",
        InventionProbability, Runs, Model.QuantityNeeded, InventionOutputRuns,
        Runs * InventionProbability, InventionOutputME, InventionOutputTE);
    public string InventionCostHint => Model.IsInvention && Runs > 0
        ? LanguageService.Format("IndustryChain_InventionCostHint", Runs)
        : string.Empty;
    public bool HasInventionWarning => Model.IsInvention
        && Runs > 0
        && Runs * InventionProbability + 0.000001 < Model.QuantityNeeded;
    public string InventionWarning => HasInventionWarning
        ? LanguageService.Format("IndustryChain_InventionWarning", Runs * InventionProbability, Model.QuantityNeeded)
        : string.Empty;
    public string DecryptorText => string.IsNullOrEmpty(DecryptorName)
        ? LanguageService.Get("IndustryPage_DecryptorNone")
        : DecryptorName;

    public double MaterialCost => Math.Max(0, Model.BuildCost - Model.JobCost);
    public double StockValue => Math.Max(0, Model.FullBuildCost - Model.BuildCost);
    public double CostSharePercent { get; set; }
    public string CostShareText => $"{CostSharePercent:0.#}%";
    public string CostShareHint => LanguageService.Format("IndustryChain_CostShareHint", CostSharePercent);
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
