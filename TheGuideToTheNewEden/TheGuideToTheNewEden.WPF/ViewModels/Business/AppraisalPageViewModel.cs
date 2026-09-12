using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 估价页：左侧粘贴物品清单与估价设置（市场星域、买卖估价口径与百分比），
/// 右侧展示明细与汇总。估价走 <see cref="AppraisalService"/>（ESI 公开市场数据），
/// 等待/结果提示走 <see cref="PageNotifyService"/>。
/// </summary>
public sealed class AppraisalPageViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _cancellationTokenSource;

    public AppraisalSetting Setting { get; private set; } = new();

    // ---------- 输入与设置 ----------

    private string _inputText = string.Empty;

    public string InputText
    {
        get => _inputText;
        set => Set(ref _inputText, value);
    }

    /// <summary>卖出估价方式的下拉索引（与 <see cref="ScalperSetting.PriceType"/> 枚举值一一对应）。变更即持久化。</summary>
    public int SellPriceTypeIndex
    {
        get => (int)Setting.SellPriceType;
        set
        {
            if (value == (int)Setting.SellPriceType)
            {
                return;
            }

            Setting.SellPriceType = (ScalperSetting.PriceType)value;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    /// <summary>买入估价方式的下拉索引。变更即持久化。</summary>
    public int BuyPriceTypeIndex
    {
        get => (int)Setting.BuyPriceType;
        set
        {
            if (value == (int)Setting.BuyPriceType)
            {
                return;
            }

            Setting.BuyPriceType = (ScalperSetting.PriceType)value;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    /// <summary>卖出估价百分比（%）。NumberBox 的 Value 可空，清空时按 100 处理。变更即持久化。</summary>
    public double? SellPercent
    {
        get => Setting.SellPercent;
        set
        {
            var clamped = Math.Clamp(value ?? 100, 0, 10000);
            if (Math.Abs(Setting.SellPercent - clamped) < 0.001)
            {
                return;
            }

            Setting.SellPercent = clamped;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    /// <summary>买入估价百分比（%）。变更即持久化。</summary>
    public double? BuyPercent
    {
        get => Setting.BuyPercent;
        set
        {
            var clamped = Math.Clamp(value ?? 100, 0, 10000);
            if (Math.Abs(Setting.BuyPercent - clamped) < 0.001)
            {
                return;
            }

            Setting.BuyPercent = clamped;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    /// <summary>历史口径统计天数。变更即持久化。</summary>
    public double? HistoryDay
    {
        get => Setting.HistoryDay;
        set
        {
            var clamped = (int)Math.Clamp(value ?? 7, 1, 365);
            if (Setting.HistoryDay == clamped)
            {
                return;
            }

            Setting.HistoryDay = clamped;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    public bool RemoveExtremum
    {
        get => Setting.RemoveExtremum;
        set
        {
            if (value == Setting.RemoveExtremum)
            {
                return;
            }

            Setting.RemoveExtremum = value;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    // ---------- 市场位置（星域 / 星系 / 建筑） ----------

    /// <summary>价格来源（由 <see cref="Views.UserControls.MarketLocationSelectorView"/> TwoWay 回写；不自动重新估价）。变更即持久化。</summary>
    public MarketLocation? SelectedMarketLocation
    {
        get => Setting.Location;
        set
        {
            if (ReferenceEquals(Setting.Location, value))
            {
                return;
            }

            Setting.Location = value;
            OnPropertyChanged();
            SaveSetting();
        }
    }

    // ---------- 结果 ----------

    public ObservableCollection<AppraisalItem> Items { get; } = [];

    private string _resultMarket = string.Empty;

    public string ResultMarket
    {
        get => _resultMarket;
        private set => Set(ref _resultMarket, value);
    }

    private string _resultVolume = string.Empty;

    public string ResultVolume
    {
        get => _resultVolume;
        private set => Set(ref _resultVolume, value);
    }

    private string _resultBuy = string.Empty;

    public string ResultBuy
    {
        get => _resultBuy;
        private set => Set(ref _resultBuy, value);
    }

    private string _resultSplit = string.Empty;

    public string ResultSplit
    {
        get => _resultSplit;
        private set => Set(ref _resultSplit, value);
    }

    private string _resultSell = string.Empty;

    public string ResultSell
    {
        get => _resultSell;
        private set => Set(ref _resultSell, value);
    }

    private bool _hasResult;

    public bool HasResult
    {
        get => _hasResult;
        private set => Set(ref _hasResult, value);
    }

    /// <summary>未识别物品名（黄色提示区）。</summary>
    private string _unresolvedText = string.Empty;

    public string UnresolvedText
    {
        get => _unresolvedText;
        private set
        {
            if (Set(ref _unresolvedText, value))
            {
                OnPropertyChanged(nameof(HasUnresolved));
            }
        }
    }

    public bool HasUnresolved => !string.IsNullOrEmpty(UnresolvedText);

    /// <summary>无报价物品（所选市场没有该物品的订单与历史）。</summary>
    private string _noPriceText = string.Empty;

    public string NoPriceText
    {
        get => _noPriceText;
        private set
        {
            if (Set(ref _noPriceText, value))
            {
                OnPropertyChanged(nameof(HasNoPrice));
            }
        }
    }

    public bool HasNoPrice => !string.IsNullOrEmpty(NoPriceText);

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    // ---------- 生命周期 ----------

    public void Init()
    {
        Setting = AppraisalSettingService.Load();
        OnPropertyChanged(nameof(SellPriceTypeIndex));
        OnPropertyChanged(nameof(BuyPriceTypeIndex));
        OnPropertyChanged(nameof(SellPercent));
        OnPropertyChanged(nameof(BuyPercent));
        OnPropertyChanged(nameof(HistoryDay));
        OnPropertyChanged(nameof(RemoveExtremum));
        OnPropertyChanged(nameof(SelectedMarketLocation));
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
    }

    // ---------- 估价 ----------

    public async Task EstimateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(InputText))
        {
            PageNotifyService.Warning(FindString("AppraisalPage_EmptyInput"));
            return;
        }

        if (Setting.Location is null)
        {
            PageNotifyService.Warning(FindString("MarketPage_UnSelectedMarket"));
            return;
        }

        SaveSetting();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        // 进度回调来自线程池线程，本地化文本先在 UI 线程取好
        var estimatingText = FindString("AppraisalPage_Estimating");
        IsBusy = true;
        PageNotifyService.ShowWaiting(estimatingText, Cancel);
        try
        {
            var result = await AppraisalService.GetEstimateAsync(
                InputText,
                Setting,
                token,
                (done, total) => PageNotifyService.UpdateWaiting($"{estimatingText}（{done}/{total}）"));
            if (result is null)
            {
                PageNotifyService.Info(FindString("General_Canceld"));
                return;
            }

            ShowResult(result);
            if (result.StructureOrdersFailed)
            {
                PageNotifyService.Warning(FindString("MarketPage_StructureOrdersFailed"));
            }

            PageNotifyService.Success(string.Format(FindString("AppraisalPage_Estimated"), result.Items.Count));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error($"{FindString("AppraisalPage_EstimateFailed")}：{ex.Message}");
        }
        finally
        {
            PageNotifyService.HideWaiting();
            IsBusy = false;
        }
    }

    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void ShowResult(AppraisalResult result)
    {
        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
            _ = item.LoadIconAsync();
        }

        ResultMarket = result.MarketName;
        ResultVolume = $"{result.TotalVolume:N2}";
        ResultBuy = $"{result.TotalBuyPrice:N2}";
        ResultSplit = $"{result.TotalSplitPrice:N2}";
        ResultSell = $"{result.TotalSellPrice:N2}";
        HasResult = result.Items.Count > 0;

        UnresolvedText = result.UnresolvedLines.Count == 0
            ? string.Empty
            : string.Format(FindString("AppraisalPage_UnresolvedLines"), string.Join("、", result.UnresolvedLines.Select(p => p.RawLine)));

        var noPrice = result.NoPriceItems;
        NoPriceText = noPrice.Count == 0
            ? string.Empty
            : string.Format(FindString("AppraisalPage_NoPriceItems"), string.Join("、", noPrice.Select(p => p.Name)));
    }

    /// <summary>把估价结果复制为文本（汇总 + 明细表）。</summary>
    public void CopyResult()
    {
        if (!HasResult)
        {
            return;
        }

        var lines = new List<string>
        {
            $"{FindString("AppraisalPage_Appraisal")} - {ResultMarket} - {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"{FindString("AppraisalPage_Buy")}: {ResultBuy}    {FindString("AppraisalPage_Split")}: {ResultSplit}    {FindString("AppraisalPage_Sell")}: {ResultSell}    {FindString("AppraisalPage_Volume")}: {ResultVolume}",
            string.Empty,
            string.Join("\t",
                FindString("AppraisalPage_Item"),
                FindString("AppraisalPage_Amount"),
                FindString("AppraisalPage_Volume"),
                FindString("AppraisalPage_UnitBuy"),
                FindString("AppraisalPage_Buy"),
                FindString("AppraisalPage_UnitSell"),
                FindString("AppraisalPage_Sell")),
        };
        foreach (var item in Items)
        {
            lines.Add(string.Join("\t",
                item.Name,
                item.Amount.ToString("N0"),
                item.Volume.ToString("N2"),
                item.UnitBuyPrice.ToString("N2"),
                item.BuyPrice.ToString("N2"),
                item.UnitSellPrice.ToString("N2"),
                item.SellPrice.ToString("N2")));
        }

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            PageNotifyService.Success(FindString("AppraisalPage_Copied"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void SaveSetting() => AppraisalSettingService.Save(Setting);

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
        Application.Current?.TryFindResource(key) as string ?? key;
}
