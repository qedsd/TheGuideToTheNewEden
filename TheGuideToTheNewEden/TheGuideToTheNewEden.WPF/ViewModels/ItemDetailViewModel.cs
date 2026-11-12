using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>物品详情窗口里的一条属性。</summary>
public sealed class ItemDetailAttributeViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
    public string UnitText { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string InternalName { get; init; } = string.Empty;

    public string ValueLine => string.IsNullOrWhiteSpace(UnitText) ? ValueText : $"{ValueText} {UnitText}";

    /// <summary>悬浮提示：说明 + 内部名（方便对照 SDE）。</summary>
    public string ToolTip => string.IsNullOrWhiteSpace(Description)
        ? InternalName
        : $"{Description}{Environment.NewLine}({InternalName})";
}

/// <summary>物品详情窗口里的一个属性分组（按 dogma 属性分类）。</summary>
public sealed class ItemDetailAttributeGroupViewModel
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<ItemDetailAttributeViewModel> Items { get; init; } = new List<ItemDetailAttributeViewModel>();
}

public sealed class ItemDetailSkillViewModel
{
    /// <summary>技能物品的 TypeID（点技能名可以继续打开它的详情）。</summary>
    public int TypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string LevelText { get; init; } = string.Empty;
}

/// <summary>
/// 物品详情（阶段 283）：数据来自 <see cref="TypeInfoService"/>（主库英文 + 本地化库中文）。
/// 属性页只展示"已发布"属性；单位里带枚举说明的（如 1=小型 2=中型 3=大型）直接显示枚举文字。
/// </summary>
public sealed class ItemDetailViewModel : INotifyPropertyChanged
{
    private static readonly Regex EnumUnitPattern = new(@"(\d+)=(.+?)(?=\s+\d+=|\s*$)", RegexOptions.Compiled);

    public ItemDetailViewModel(int typeId)
    {
        TypeId = typeId;
        var info = TypeInfoService.GetTypeInfo(typeId);
        Found = info != null;
        if (info == null)
        {
            Title = LanguageService.Get("ItemDetail_NoItem");
            return;
        }

        Title = info.Name;
        IconImage = LoadIcon(info.TypeID);
        Description = info.Description;
        CategoryText = string.Join(" · ", new[] { info.CategoryName, info.GroupName }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        MetaLevelText = info.MetaLevel.ToString();
        VolumeText = FormatVolume(info.Volume);
        PackagedVolumeText = FormatVolume(info.PackagedVolume);
        MassText = FormatMass(info.Mass);
        CapacityText = FormatVolume(info.Capacity);
        RadiusText = $"{info.Radius:N0} m";
        PortionSizeText = info.PortionSize.ToString("N0");
        BasePriceText = info.BasePrice > 0 ? $"{info.BasePrice:N2} ISK" : "—";
        MarketGroupText = string.IsNullOrWhiteSpace(info.MarketGroupName) ? "—" : info.MarketGroupName;
        TypeIdText = info.TypeID.ToString();
        PublishedText = LanguageService.Get(info.Published ? "ItemDetail_Published" : "ItemDetail_Unpublished");

        BuildAttributes(info);
        Skills = new ObservableCollection<ItemDetailSkillViewModel>(info.Skills.Select(p => new ItemDetailSkillViewModel
        {
            TypeId = p.SkillTypeID,
            Name = p.Name,
            LevelText = LanguageService.Format("ItemDetail_SkillLevel", p.Level),
        }));
        HasSkills = Skills.Count > 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int TypeId { get; }
    public bool Found { get; }
    public string Title { get; private set; } = string.Empty;
    /// <summary>物品图标（ESI 图片服务，异步加载；取不到时为空）。</summary>
    public ImageSource? IconImage { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string CategoryText { get; private set; } = string.Empty;
    public string MetaLevelText { get; private set; } = string.Empty;
    public string VolumeText { get; private set; } = string.Empty;
    public string PackagedVolumeText { get; private set; } = string.Empty;
    public string MassText { get; private set; } = string.Empty;
    public string CapacityText { get; private set; } = string.Empty;
    public string RadiusText { get; private set; } = string.Empty;
    public string PortionSizeText { get; private set; } = string.Empty;
    public string BasePriceText { get; private set; } = string.Empty;
    public string MarketGroupText { get; private set; } = string.Empty;
    public string TypeIdText { get; private set; } = string.Empty;
    public string PublishedText { get; private set; } = string.Empty;

    public ObservableCollection<ItemDetailAttributeGroupViewModel> AttributeGroups { get; } = new();
    public ObservableCollection<ItemDetailSkillViewModel> Skills { get; } = new();

    public bool HasSkills { get; }

    public bool HasAttributes => AttributeGroups.Count > 0;

    private void BuildAttributes(TypeInfo info)
    {
        var typeNames = new Dictionary<int, string>();
        var groups = info.Attributes
            .Where(p => p.Published)
            .GroupBy(p => p.CategoryName ?? string.Empty, StringComparer.Ordinal)
            .Select(g => new ItemDetailAttributeGroupViewModel
            {
                Name = g.Key,
                Items = g.Select(attr =>
                {
                    ResolveValue(attr, typeNames, out var valueText, out var unitText);
                    return new ItemDetailAttributeViewModel
                    {
                        DisplayName = attr.DisplayName,
                        ValueText = valueText,
                        UnitText = unitText,
                        Description = attr.Description,
                        InternalName = attr.Name,
                    };
                }).ToList(),
            })
            .ToList();

        _allGroups.Clear();
        _allGroups.AddRange(groups);
        ApplyPublishedFilter();
    }

    private void ApplyPublishedFilter()
    {
        AttributeGroups.Clear();
        foreach (var group in _allGroups)
        {
            if (group.Items.Count == 0) continue;
            AttributeGroups.Add(group);
        }
        OnPropertyChanged(nameof(HasAttributes));
    }

    private readonly List<ItemDetailAttributeGroupViewModel> _allGroups = new();

    /// <summary>
    /// 把属性值 + 单位变成可读文本：
    /// 百分比 ×100；typeID 型解析成名称；单位里带枚举说明的（1=小型 2=中型…）直接换成枚举文字；
    /// 其余"Fitting slots"这类没有本地化、又不是符号的单位直接不显示。
    /// </summary>
    private static void ResolveValue(TypeInfoAttribute attr, Dictionary<int, string> typeNames,
        out string valueText, out string unitText)
    {
        if (attr.IsTypeReference)
        {
            var id = (int)Math.Round(attr.Value);
            unitText = string.Empty;
            if (id > 0)
            {
                if (!typeNames.TryGetValue(id, out var name))
                {
                    name = InvTypeService.QueryType(id)?.TypeName ?? "#" + id;
                    typeNames[id] = name;
                }
                valueText = name;
            }
            else
            {
                valueText = "—";
            }
            return;
        }

        valueText = FormatNumber(attr.IsPercent ? attr.Value * 100 : attr.Value);

        if (attr.UnitText.IndexOf('=') >= 0)
        {
            var label = ResolveEnumLabel(attr.UnitText, attr.Value);
            if (!string.IsNullOrEmpty(label))
            {
                valueText = label;
            }
            unitText = string.Empty;
            return;
        }

        // "Fitting slots"、"Ore units" 这类没有本地化的词组单位：不显示单位，只留数字
        unitText = attr.UnitText.IndexOf(' ') >= 0 ? string.Empty : attr.UnitText;
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value) >= 1000)
        {
            return value.ToString("N0");
        }
        if (Math.Abs(value - Math.Round(value)) < 0.0001)
        {
            return ((long)Math.Round(value)).ToString("N0");
        }
        return value.ToString("0.###");
    }

    private static string ResolveEnumLabel(string unitText, double value)
    {
        foreach (Match match in EnumUnitPattern.Matches(unitText))
        {
            if (int.TryParse(match.Groups[1].Value, out var key) && Math.Abs(key - value) < 0.0001)
            {
                var label = match.Groups[2].Value.Trim();
                if (string.Equals(label, "True", StringComparison.OrdinalIgnoreCase))
                {
                    return LanguageService.Get("General_Yes");
                }
                if (string.Equals(label, "False", StringComparison.OrdinalIgnoreCase))
                {
                    return LanguageService.Get("General_No");
                }
                return label;
            }
        }
        return string.Empty;
    }

    /// <summary>异步加载 64px 物品图标：不阻塞 UI，失败时返回 null（窗口显示空白占位）。</summary>
    private static ImageSource? LoadIcon(int typeId)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"https://images.evetech.net/types/{typeId}/icon?size=64", UriKind.Absolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();   // 默认 OnDemand：远程下载在后台进行
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string FormatVolume(double value)
        => value <= 0 ? "—" : $"{value:N2} m³";

    private static string FormatMass(double value)
        => value <= 0 ? "—" : $"{value:N0} kg";

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

