using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>
/// 行星计划「产物链」的一个依赖节点：目标是根，配方输入逐层展开成子节点
/// （自产 → 中间产物，未安排生产的输入 → 外购 / P0 提取原料）。
/// 与制造计划的 BOM / 成本视图同一套「扁平列表 + 缩进 + 展开收起」做法，
/// 不用 TreeView 以免缩进把节点卡片挤窄。
/// </summary>
public sealed class PlanetaryProductNodeViewModel : INotifyPropertyChanged
{
    /// <summary>计划内实际生产（或作为目标）的产物节点。</summary>
    public PlanetaryProductNodeViewModel(PlanetaryProductPlan product, int depth)
    {
        TypeId = product.TypeId;
        Name = product.Name;
        Tier = product.Tier;
        RequiredPerDay = product.RequiredPerDay;
        OutputPerDay = product.OutputPerDay;
        Factories = product.Factories;
        Colonies = product.Colonies;
        PlanetType = product.PlanetType;
        SetupCost = product.SetupCost;
        InputsText = product.Inputs;
        IsTarget = product.IsTarget;
        IsProduced = true;
        Depth = depth;
        IndentMargin = new Thickness(depth * 18, 0, 0, 0);
    }

    /// <summary>计划里没有安排生产的下游输入（外购 / P0 原料）。</summary>
    public PlanetaryProductNodeViewModel(
        int typeId,
        string name,
        PlanetaryProductTier tier,
        double requiredPerDay,
        PlanetaryImportPlan? import,
        int depth)
    {
        TypeId = typeId;
        Name = name;
        Tier = tier;
        RequiredPerDay = requiredPerDay;
        IsProduced = false;
        IsImported = import is not null;
        ImportUnitPrice = import?.UnitPrice ?? 0;
        ImportCostPerDay = import?.CostPerDay ?? 0;
        InputsText = import is not null
            ? string.Format("外购合计 {0:N1}/天 · 成本 {1:N0} ISK/天", import.QuantityPerDay, import.CostPerDay)
            : tier == PlanetaryProductTier.P0
                ? "P0 原料 · 由提取器采集"
                : "计划未安排生产 · 需外购";
        Depth = depth;
        IndentMargin = new Thickness(depth * 18, 0, 0, 0);
    }

    public ObservableCollection<PlanetaryProductNodeViewModel> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;

    public int TypeId { get; }
    public string Name { get; }
    public PlanetaryProductTier Tier { get; }
    public double RequiredPerDay { get; }
    public double OutputPerDay { get; }
    public int Factories { get; }
    public int Colonies { get; }
    public string PlanetType { get; } = string.Empty;
    public double SetupCost { get; }
    public double ImportUnitPrice { get; }
    public double ImportCostPerDay { get; }
    public string InputsText { get; }

    /// <summary>计划内生产的目标产物。</summary>
    public bool IsTarget { get; }

    /// <summary>计划内生产、但不是目标的中间产物。</summary>
    public bool IsProduced { get; }

    /// <summary>计划里没有生产、需要采购的输入。</summary>
    public bool IsImported { get; }

    /// <summary>XAML 触发器用的四种角色（互斥）。</summary>
    public bool IsBuild => IsProduced && !IsTarget;
    public bool IsImport => !IsProduced && (IsImported || Tier != PlanetaryProductTier.P0);
    public bool IsRaw => !IsProduced && !IsImport;

    public string TierText => "P" + (int)Tier;
    public string DecisionText => IsTarget ? "目标" : IsProduced ? "自产" : IsImport ? "外购" : "提取";
    public string RequiredText => RequiredPerDay > 0 ? string.Format("需求 {0:N1}/天", RequiredPerDay) : "—";

    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (OutputPerDay > 0) parts.Add(string.Format("产出 {0:N1}/天", OutputPerDay));
            if (Factories > 0) parts.Add(Factories + " 工厂");
            if (Colonies > 0) parts.Add(Colonies + " 殖民地");
            if (!string.IsNullOrWhiteSpace(PlanetType)) parts.Add(PlanetType);
            if (SetupCost > 0) parts.Add(string.Format("建造 {0:N0} ISK", SetupCost));
            if (ImportUnitPrice > 0) parts.Add(string.Format("单价 {0:N2}", ImportUnitPrice));
            if (ImportCostPerDay > 0) parts.Add(string.Format("日成本 {0:N0} ISK", ImportCostPerDay));
            return string.Join(" · ", parts);
        }
    }

    public bool HasMetaText => MetaText.Length > 0;
    public bool HasInputsText => !string.IsNullOrWhiteSpace(InputsText);
    public string TooltipText => HasMetaText ? MetaText + "\n" + InputsText : InputsText;

    public int Depth { get; }
    public Thickness IndentMargin { get; }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
