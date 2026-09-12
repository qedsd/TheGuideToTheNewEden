using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 购物车页：统计合计、复制为游戏内多件物品订单、从剪贴板回填剩余数量、保存为购物记录。
/// 数据源是 <see cref="BusinessService.ShoppingCart"/>（与倒货页/记录页共享）。
/// </summary>
public sealed class ScalperShoppingCartViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ScalperShoppingItem> Items => BusinessService.Current.ShoppingCart;

    public bool HasItems => Items.Count > 0;

    private double _roi;
    private double _netProfit;
    private double _principal;
    private double _volume;
    private double _iskPerJump;
    private double _iskPerVolume;
    private int _typeCount;

    public double ROI { get => _roi; private set => Set(ref _roi, value); }
    public double NetProfit { get => _netProfit; private set => Set(ref _netProfit, value); }
    public double Principal { get => _principal; private set => Set(ref _principal, value); }
    public double Volume { get => _volume; private set => Set(ref _volume, value); }
    public double IskPerJump { get => _iskPerJump; private set => Set(ref _iskPerJump, value); }
    public double IskPerVolume { get => _iskPerVolume; private set => Set(ref _iskPerVolume, value); }
    public int TypeCount { get => _typeCount; private set => Set(ref _typeCount, value); }

    private string? _message;

    /// <summary>最近一次操作的提示（复制/粘贴/保存结果）。</summary>
    public string? Message { get => _message; private set => Set(ref _message, value); }

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public void Init()
    {
        Items.CollectionChanged -= OnItemsChanged;
        Items.CollectionChanged += OnItemsChanged;
        BusinessService.Current.TypeCountChanged -= OnTypeCountChanged;
        BusinessService.Current.TypeCountChanged += OnTypeCountChanged;
        TrackItems();
        Recalculate();
    }

    public void Dispose()
    {
        Items.CollectionChanged -= OnItemsChanged;
        BusinessService.Current.TypeCountChanged -= OnTypeCountChanged;
        foreach (var item in Items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TrackItems();
        Recalculate();
    }

    private void TrackItems()
    {
        foreach (var item in Items)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => Recalculate();

    private void OnTypeCountChanged(List<(Core.DBModels.InvType Type, long Count)> types)
    {
        UpdateItems(types.ToDictionary(p => p.Type.TypeID, p => p.Count));
    }

    public void Recalculate()
    {
        OnPropertyChanged(nameof(HasItems));
        if (Items.Count == 0)
        {
            ROI = 0;
            NetProfit = 0;
            Principal = 0;
            Volume = 0;
            IskPerJump = 0;
            IskPerVolume = 0;
            TypeCount = 0;
            return;
        }

        NetProfit = Items.Sum(p => p.NetProfit);
        Principal = Items.Sum(p => p.BuyPrice * p.Quantity);
        ROI = Principal == 0 ? 0 : NetProfit / Principal * 100;
        Volume = Items.Sum(p => p.Volume);
        IskPerJump = Items.Average(p => p.IskPerJump);
        IskPerVolume = Items.Average(p => p.IskPerVolume);
        TypeCount = Items.Count;
    }

    public void Remove(IEnumerable<ScalperShoppingItem> items)
    {
        foreach (var item in items.ToList())
        {
            Items.Remove(item);
        }
    }

    /// <summary>复制为游戏内"多件物品"订单文本（每行"物品名 数量"）。</summary>
    public string BuildGameOrderText()
    {
        var builder = new StringBuilder();
        foreach (var item in Items)
        {
            builder.Append(item.InvType.TypeName);
            builder.Append(' ');
            builder.Append(item.Quantity);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>复制成功提示。</summary>
    public void NotifyCopied()
    {
        SetMessage(FindString("BusinessPage_CopyToGameOrder_Success"));
    }

    /// <summary>
    /// 从游戏内"多件物品"订单文本回填剩余数量：每行逗号分隔，第 1 列为物品 ID、第 14 列为剩余数量。
    /// </summary>
    public void PasteFromGameOrderText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            SetMessage(FindString("BusinessPage_NotPasteItem"));
            return;
        }

        var remain = new Dictionary<int, long>();
        foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var array = line.Split(',');
                if (array.Length >= 24)
                {
                    remain[int.Parse(array[1])] = (int)float.Parse(array[14]);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        SetMessage(remain.Count == 0 ? FindString("BusinessPage_NotPasteItem") : UpdateItems(remain));
    }

    private string UpdateItems(Dictionary<int, long> remain)
    {
        var updated = 0;
        var removed = new List<ScalperShoppingItem>();
        foreach (var item in Items)
        {
            if (remain.TryGetValue(item.InvType.TypeID, out var count))
            {
                updated++;
                item.Quantity -= count;
                if (item.Quantity <= 0)
                {
                    removed.Add(item);
                }
            }
        }

        foreach (var item in removed)
        {
            Items.Remove(item);
        }

        return updated > 0
            ? $"{FindString("BusinessPage_UpdatedScalperShoppingItem1")}{updated}{FindString("BusinessPage_UpdatedScalperShoppingItem2")}"
            : FindString("BusinessPage_NoUpdatedScalperShoppingItem");
    }

    public void SaveToRecord()
    {
        if (!HasItems)
        {
            SetMessage(FindString("BusinessPage_ShoppingCartEmpty"));
            return;
        }

        ShoppingRecordService.Current.Add(Items.ToList());
        SetMessage(FindString("General_SaveSuccess"));
    }

    /// <summary>编辑后刷新合计。</summary>
    public void OnItemEdited() => Recalculate();

    private void SetMessage(string? text)
    {
        Message = text;
        OnPropertyChanged(nameof(HasMessage));
    }

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
