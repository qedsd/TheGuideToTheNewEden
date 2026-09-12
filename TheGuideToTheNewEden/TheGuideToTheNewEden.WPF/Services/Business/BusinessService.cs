using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 倒货功能跨页共享状态：
/// <list type="bullet">
///   <item>倒货排除清单——市场/订单页右键"添加到倒货排除列表"，倒货页实时同步；</item>
///   <item>物品数量变化通知——购物车按游戏内订单粘贴/外部通知扣减数量。</item>
/// </list>
/// 进程内单例，移植自 WinUI 版 <c>Services/BusinessService</c>。
/// </summary>
public sealed class BusinessService
{
    private static BusinessService? _current;

    public static BusinessService Current => _current ??= new BusinessService();

    private BusinessService()
    {
    }

    // ---------- 倒货过滤清单 ----------

    private readonly Dictionary<int, InvType> _filteredTypes = [];

    public void AddToFilter(IEnumerable<InvType> types)
    {
        var changed = new List<InvType>();
        foreach (var type in types)
        {
            if (_filteredTypes.TryAdd(type.TypeID, type))
            {
                changed.Add(type);
            }
        }

        if (changed.Count > 0)
        {
            FilterChanged?.Invoke(changed, true);
        }
    }

    public void RemoveFromFilter(IEnumerable<InvType> types)
    {
        var changed = new List<InvType>();
        foreach (var type in types)
        {
            if (_filteredTypes.Remove(type.TypeID))
            {
                changed.Add(type);
            }
        }

        if (changed.Count > 0)
        {
            FilterChanged?.Invoke(changed, false);
        }
    }

    public List<InvType> GetFilterTypes() => _filteredTypes.Values.ToList();

    /// <summary>参数为 (变化的物品, 是否为新增)。</summary>
    public event Action<List<InvType>, bool>? FilterChanged;

    // ---------- 物品数量变化通知 ----------

    public void NotifyTypeCountChanged(List<(InvType Type, long Count)> types)
    {
        if (types.Count > 0)
        {
            TypeCountChanged?.Invoke(types);
        }
    }

    public event Action<List<(InvType Type, long Count)>>? TypeCountChanged;

    // ---------- 倒货购物车 ----------

    /// <summary>
    /// 倒货购物车。放在服务层是为了让"倒货/购物车/记录"三个子页共享同一份数据
    /// （WinUI 版各页各自持有，靠 Page 事件手工传递）。
    /// </summary>
    public ObservableCollection<ScalperShoppingItem> ShoppingCart { get; } = [];
}
