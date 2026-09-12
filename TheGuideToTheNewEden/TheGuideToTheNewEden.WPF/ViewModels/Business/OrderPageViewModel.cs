using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 订单页：某角色的个人/军团未结订单，以及与市场参考价的差值。
/// 两个过滤器（订单类型 卖/买、来源 个人/军团）与 WinUI 版一致。
/// </summary>
public sealed class OrderPageViewModel : INotifyPropertyChanged
{
    /// <summary>当前来源的全部订单（未按买卖过滤）。</summary>
    private readonly List<StatusOrder> _allOrders = [];

    private CharacterContext? _context;

    public OrderPageViewModel()
    {
        // 角色被移除时清掉选中项，避免继续用已失效的令牌取数
        CharacterStore.Changed += (_, _) =>
        {
            if (SelectedCharacter is not null && !Characters.Contains(SelectedCharacter))
            {
                SelectedCharacter = null;
            }
        };
    }

    /// <summary>已授权角色（直接暴露 <see cref="CharacterStore.Characters"/>，增删自动刷新）。</summary>
    public ObservableCollection<AuthorizedCharacterData> Characters => CharacterStore.Characters;

    private AuthorizedCharacterData? _selectedCharacter;

    public AuthorizedCharacterData? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (Set(ref _selectedCharacter, value))
            {
                _context = null;
                _allOrders.Clear();
                Orders.Clear();
                ErrorMessage = null;
            }
        }
    }

    /// <summary>0 = 卖单，1 = 买单。</summary>
    private int _orderTypeIndex;

    public int OrderTypeIndex
    {
        get => _orderTypeIndex;
        set
        {
            if (Set(ref _orderTypeIndex, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>0 = 个人，1 = 军团。</summary>
    private int _orderFromIndex;

    public int OrderFromIndex
    {
        get => _orderFromIndex;
        set
        {
            if (Set(ref _orderFromIndex, value))
            {
                _allOrders.Clear();
                Orders.Clear();
            }
        }
    }

    /// <summary>当前显示的订单（已按买卖过滤）。</summary>
    public ObservableCollection<StatusOrder> Orders { get; } = [];

    private bool _isLoading;
    private string _statusText = string.Empty;
    private string? _errorMessage;

    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
            }
        }
    }

    /// <summary>是否有错误/提示信息需要展示。</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>有角色时默认选第一个，省去每次都要手选。</summary>
    public void EnsureDefaultCharacter()
    {
        if (SelectedCharacter is null && Characters.Count > 0)
        {
            SelectedCharacter = Characters[0];
        }
    }

    /// <summary>按当前来源拉取订单并计算与市场参考价的差值。</summary>
    public async Task LoadAsync()
    {
        if (SelectedCharacter is null)
        {
            ErrorMessage = FindString("General_CharacterUnselected");
            _allOrders.Clear();
            Orders.Clear();
            return;
        }

        _context = new CharacterContext(SelectedCharacter);
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            StatusText = FindString("MarketPage_GettingOrder");
            var orders = OrderFromIndex == 0
                ? await CharacterOrderService.GetCharacterOrdersAsync(_context)
                : await CharacterOrderService.GetCorpOrdersAsync(_context);

            _allOrders.Clear();

            if (orders is null)
            {
                if (OrderFromIndex == 1)
                {
                    ErrorMessage = FindString("OrderPage_CorpOrdersFailed");
                }
            }
            else if (orders.Count == 0)
            {
                ErrorMessage = FindString("OrderPage_NoOrder");
            }
            else
            {
                StatusText = FindString("OrderPage_Calculating");
                _allOrders.AddRange(await CharacterOrderService.BuildStatusAsync(orders));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            StatusText = string.Empty;
        }
    }

    private void ApplyFilter()
    {
        Orders.Clear();
        var wantBuy = OrderTypeIndex == 1;
        foreach (var order in _allOrders.Where(p => p.Target.IsBuyOrder == wantBuy))
        {
            Orders.Add(order);
        }
    }

    /// <summary>
    /// 生成"复制为游戏批量购买"的文本：只取**被压单/未知**（<c>Normal != true</c>）的行，
    /// 每行 `物品名 1`（与 WinUI 版一致）。无可复制内容时返回 null。
    /// </summary>
    public static string? BuildGameOrderText(IEnumerable<StatusOrder> selected)
    {
        var lines = selected
            .Where(p => p.Normal != true)
            .Select(p => p.Target.InvType?.TypeName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name + " 1")
            .ToList();

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    /// <summary>在游戏客户端打开该物品的市场详情。</summary>
    public async Task<bool> ShowInGameAsync(StatusOrder order)
    {
        if (_context is null)
        {
            return false;
        }

        return await CharacterOrderService.OpenMarketDetailsAsync(_context, order.Target.TypeId);
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
