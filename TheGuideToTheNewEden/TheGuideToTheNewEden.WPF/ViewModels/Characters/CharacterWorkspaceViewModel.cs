using System.ComponentModel;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>
/// 角色工作区左侧信息栏（与 WinUI3 CharacterPage 左栏同构）：
/// 身份/资产卡 + 技能队列卡 + ZKB 卡，数据来自共享的 <see cref="CharacterCardViewModel"/> 与总览服务缓存。
/// </summary>
public sealed class CharacterWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly CharacterContext _context;

    private string _birthdayText = "-";
    private string _totalSpText = "-";
    private string _unallocatedSpText = "0";
    private string _lpText = "0";
    private string _securityText = "-";
    private string _walletText = "-";
    private string _corpWalletText = "-";
    private bool _hasCorpWallet;
    private string _corporationText = "-";
    private string _allianceTickerText = string.Empty;
    private bool _hasAlliance;

    private bool _hasZkb;
    private int _dangerRatio;
    private int _gangRatio;
    private int _itemDestroyed;
    private string _iskDestroyedText = "0";
    private int _itemLost;
    private string _iskLostText = "0";

    public CharacterWorkspaceViewModel(CharacterCardViewModel card)
    {
        Card = card;
        _context = new CharacterContext(card.Character);
    }

    /// <summary>卡片视图模型（头像、名称、钱包、技能队列等复用其已加载的数据）。</summary>
    public CharacterCardViewModel Card { get; }

    public string BirthdayText
    {
        get => _birthdayText;
        private set => Set(ref _birthdayText, value);
    }

    public string TotalSpText
    {
        get => _totalSpText;
        private set => Set(ref _totalSpText, value);
    }

    public string UnallocatedSpText
    {
        get => _unallocatedSpText;
        private set => Set(ref _unallocatedSpText, value);
    }

    public string LpText
    {
        get => _lpText;
        private set => Set(ref _lpText, value);
    }

    public string SecurityText
    {
        get => _securityText;
        private set => Set(ref _securityText, value);
    }

    public string WalletText
    {
        get => _walletText;
        private set => Set(ref _walletText, value);
    }

    public string CorpWalletText
    {
        get => _corpWalletText;
        private set => Set(ref _corpWalletText, value);
    }

    /// <summary>军团钱包需要财务权限，取不到时整行不显示（与 WinUI 一致）。</summary>
    public bool HasCorpWallet
    {
        get => _hasCorpWallet;
        private set => Set(ref _hasCorpWallet, value);
    }

    public string CorporationText
    {
        get => _corporationText;
        private set => Set(ref _corporationText, value);
    }

    public string AllianceTickerText
    {
        get => _allianceTickerText;
        private set => Set(ref _allianceTickerText, value);
    }

    public bool HasAlliance
    {
        get => _hasAlliance;
        private set => Set(ref _hasAlliance, value);
    }

    public bool HasZkb
    {
        get => _hasZkb;
        private set => Set(ref _hasZkb, value);
    }

    public int DangerRatio
    {
        get => _dangerRatio;
        private set => Set(ref _dangerRatio, value);
    }

    public int GangRatio
    {
        get => _gangRatio;
        private set => Set(ref _gangRatio, value);
    }

    public int ItemDestroyed
    {
        get => _itemDestroyed;
        private set => Set(ref _itemDestroyed, value);
    }

    public string IskDestroyedText
    {
        get => _iskDestroyedText;
        private set => Set(ref _iskDestroyedText, value);
    }

    public int ItemLost
    {
        get => _itemLost;
        private set => Set(ref _itemLost, value);
    }

    public string IskLostText
    {
        get => _iskLostText;
        private set => Set(ref _iskLostText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>刷新左栏（含 ZKB）。视图/技能走服务缓存，重复进入不会重复请求 ESI。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        // 卡片模型负责头像与卡片级数据；两个服务都带 TTL 缓存，不会产生额外请求。
        await Card.LoadAsync(forceRefresh);

        var overview = await CharacterOverviewService.GetAsync(_context, forceRefresh);
        if (overview is not null)
        {
            BirthdayText = overview.Birthday is { } birthday
                ? ToLocal(birthday).ToString("yyyy.MM.dd HH:mm")
                : "-";
            SecurityText = overview.SecurityStatus.ToString("0.0");
            WalletText = overview.WalletBalance.ToString("N2");
            LpText = overview.LoyaltyPoints.ToString("N0");
            CorporationText = overview.CorporationName ?? "-";
            HasAlliance = !string.IsNullOrWhiteSpace(overview.AllianceTicker);
            AllianceTickerText = overview.AllianceTicker ?? string.Empty;
            HasCorpWallet = overview.CorporationWalletBalance is not null;
            CorpWalletText = overview.CorporationWalletBalance?.ToString("N2") ?? "-";
        }

        var skills = await CharacterSkillService.GetAsync(_context, forceRefresh);
        if (skills is not null)
        {
            TotalSpText = skills.TotalSkillPoints.ToString("N0");
            UnallocatedSpText = skills.UnallocatedSkillPoints.ToString("N0");
        }

        await LoadZkbAsync();
    }

    /// <summary>ZKB 战绩统计（失败保持不显示该卡）。</summary>
    public async Task LoadZkbAsync()
    {
        try
        {
            var statistic = await ZKB.NET.ZKB.GetStatisticAsync(ZKB.NET.EntityType.CharacterID, (int)Card.CharacterId);
            if (statistic is null)
            {
                return;
            }

            HasZkb = true;
            DangerRatio = statistic.DangerRatio;
            GangRatio = statistic.GangRatio;
            ItemDestroyed = statistic.ItemDestroyed;
            IskDestroyedText = FormatIskShort(statistic.ISKDestroyed);
            ItemLost = statistic.ItemLost;
            IskLostText = FormatIskShort(statistic.ISKLost);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static DateTime ToLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Local => value,
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
    };

    /// <summary>ISK 缩写（WinUI 的 ISKNormalizeConverter 同款效果）。</summary>
    private static string FormatIskShort(double value)
    {
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000_000_000 => $"{value / 1_000_000_000_000:0.##}T",
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B",
            >= 1_000_000 => $"{value / 1_000_000:0.##}M",
            >= 1_000 => $"{value / 1_000:0.##}K",
            _ => $"{value:0.##}",
        };
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
