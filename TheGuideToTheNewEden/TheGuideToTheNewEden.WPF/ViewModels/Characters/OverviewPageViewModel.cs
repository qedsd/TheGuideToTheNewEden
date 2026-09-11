using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>技能队列单条状态，语义与 WinUI <c>Core.Models.Character.SkillQueueItem</c> 一致。</summary>
public enum OverviewSkillStatus
{
    Finished,
    Running,
    Waiting,
    Pause,
}

/// <summary>总览页技能队列行（技能名 / 目标等级 / 状态 / 剩余时间 / 起止时间）。</summary>
public sealed class OverviewSkillQueueItemViewModel
{
    public string SkillName { get; init; } = string.Empty;

    public int FinishedLevel { get; init; }

    public string RemainTime { get; init; } = string.Empty;

    /// <summary>开始时间（UTC，展示按 WinUI 用 UTC）。</summary>
    public string StartText { get; init; } = string.Empty;

    /// <summary>结束时间（UTC）。</summary>
    public string FinishText { get; init; } = string.Empty;

    /// <summary>开始时间的本地时间（鼠标悬停提示）。</summary>
    public string StartTooltip { get; init; } = string.Empty;

    /// <summary>结束时间的本地时间（鼠标悬停提示）。</summary>
    public string FinishTooltip { get; init; } = string.Empty;

    public OverviewSkillStatus Status { get; init; }

    public bool IsFinished => Status == OverviewSkillStatus.Finished;

    public bool IsRunning => Status == OverviewSkillStatus.Running;

    public bool IsWaiting => Status == OverviewSkillStatus.Waiting;

    public bool IsPause => Status == OverviewSkillStatus.Pause;
}

/// <summary>角色总览页视图模型：军团/联盟、舰船/位置、在线状态与技能队列。</summary>
public sealed class OverviewPageViewModel : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    private string _characterName = string.Empty;
    private string _corporationName = string.Empty;
    private string _corporationTicker = string.Empty;
    private ImageSource? _corporationLogo;
    private bool _hasCorporation;
    private string _allianceName = string.Empty;
    private string _allianceTicker = string.Empty;
    private ImageSource? _allianceLogo;
    private bool _hasAlliance;
    private string _shipName = string.Empty;
    private string _shipTypeName = string.Empty;
    private ImageSource? _shipLogo;
    private bool _hasShip;
    private string _locationName = string.Empty;
    private string _systemName = string.Empty;
    private string _systemSecurityText = string.Empty;
    private bool _hasLocation;
    private bool _online;
    private string _onlineStatusText = string.Empty;
    private string _lastLoginText = string.Empty;
    private string _lastLoginTooltip = string.Empty;
    private int _loginCount;
    private bool _hasSkillQueue;

    public string CharacterName
    {
        get => _characterName;
        private set => Set(ref _characterName, value);
    }

    public string CorporationName
    {
        get => _corporationName;
        private set => Set(ref _corporationName, value);
    }

    public string CorporationTicker
    {
        get => _corporationTicker;
        private set => Set(ref _corporationTicker, value);
    }

    public ImageSource? CorporationLogo
    {
        get => _corporationLogo;
        private set => Set(ref _corporationLogo, value);
    }

    public bool HasCorporation
    {
        get => _hasCorporation;
        private set => Set(ref _hasCorporation, value);
    }

    public string AllianceName
    {
        get => _allianceName;
        private set => Set(ref _allianceName, value);
    }

    public string AllianceTicker
    {
        get => _allianceTicker;
        private set => Set(ref _allianceTicker, value);
    }

    public ImageSource? AllianceLogo
    {
        get => _allianceLogo;
        private set => Set(ref _allianceLogo, value);
    }

    public bool HasAlliance
    {
        get => _hasAlliance;
        private set => Set(ref _hasAlliance, value);
    }

    public string ShipName
    {
        get => _shipName;
        private set => Set(ref _shipName, value);
    }

    public string ShipTypeName
    {
        get => _shipTypeName;
        private set => Set(ref _shipTypeName, value);
    }

    public ImageSource? ShipLogo
    {
        get => _shipLogo;
        private set => Set(ref _shipLogo, value);
    }

    public bool HasShip
    {
        get => _hasShip;
        private set => Set(ref _hasShip, value);
    }

    /// <summary>空间站 / 建筑名称。</summary>
    public string LocationName
    {
        get => _locationName;
        private set => Set(ref _locationName, value);
    }

    public string SystemName
    {
        get => _systemName;
        private set => Set(ref _systemName, value);
    }

    /// <summary>星系安全等级，保留两位小数。</summary>
    public string SystemSecurityText
    {
        get => _systemSecurityText;
        private set => Set(ref _systemSecurityText, value);
    }

    public bool HasLocation
    {
        get => _hasLocation;
        private set => Set(ref _hasLocation, value);
    }

    /// <summary>舰船或位置任一有数据即显示"舰船/位置"卡片。</summary>
    public bool HasShipOrLocation => _hasShip || _hasLocation;

    public bool Online
    {
        get => _online;
        private set => Set(ref _online, value);
    }

    public string OnlineStatusText
    {
        get => _onlineStatusText;
        private set => Set(ref _onlineStatusText, value);
    }

    /// <summary>距最近登录的时长（如 "2d 3.5h"，与 WinUI 同款格式）。</summary>
    public string LastLoginText
    {
        get => _lastLoginText;
        private set => Set(ref _lastLoginText, value);
    }

    public string LastLoginTooltip
    {
        get => _lastLoginTooltip;
        private set => Set(ref _lastLoginTooltip, value);
    }

    public int LoginCount
    {
        get => _loginCount;
        private set => Set(ref _loginCount, value);
    }

    public bool HasSkillQueue
    {
        get => _hasSkillQueue;
        private set => Set(ref _hasSkillQueue, value);
    }

    public ObservableCollection<OverviewSkillQueueItemViewModel> SkillQueue { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>加载总览 / 补充信息 / 技能队列（forceRefresh=true 时绕过缓存）。</summary>
    public async Task LoadAsync(CharacterContext context, bool forceRefresh)
    {
        CharacterName = context.Name;

        var overview = await CharacterOverviewService.GetAsync(context, forceRefresh);
        var extra = await CharacterOverviewExtraService.GetAsync(context, forceRefresh);
        var skills = await CharacterSkillService.GetAsync(context, forceRefresh);

        ApplyOverview(overview, extra);

        // 图片与文本数据解耦，先出文字再异步补图。
        if (overview is not null)
        {
            _ = LoadCorporationLogoAsync(overview.CorporationId);
            _ = LoadAllianceLogoAsync(overview.AllianceId);
        }

        if (overview is not null)
        {
            _ = LoadShipLogoAsync(overview.ShipTypeId);
        }

        ApplySkillQueue(skills?.Queue);
    }

    private void ApplyOverview(CharacterOverview? overview, CharacterOverviewExtra? extra)
    {
        if (overview is not null)
        {
            HasCorporation = !string.IsNullOrEmpty(overview.CorporationName);
            CorporationName = overview.CorporationName ?? string.Empty;
            CorporationTicker = overview.CorporationTicker ?? string.Empty;

            HasAlliance = !string.IsNullOrEmpty(overview.AllianceName);
            AllianceName = overview.AllianceName ?? string.Empty;
            AllianceTicker = overview.AllianceTicker ?? string.Empty;

            ShipTypeName = overview.ShipTypeName ?? string.Empty;

            Online = overview.Online;
            OnlineStatusText = FindString(overview.Online ? "CharacterPage_Online" : "CharacterPage_Offline");
            LoginCount = overview.LoginCount;
            var lastLogin = ToUtc(overview.LastLogin);
            LastLoginText = lastLogin is null ? "-" : FormatDuration(DateTime.UtcNow - lastLogin.Value);
            LastLoginTooltip = lastLogin?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
        }

        if (extra is not null)
        {
            ShipName = extra.ShipName ?? string.Empty;
            LocationName = extra.LocationName ?? string.Empty;
            SystemName = extra.SolarSystemName ?? string.Empty;
            SystemSecurityText = extra.SecurityStatus?.ToString("N2") ?? string.Empty;
        }

        HasShip = !string.IsNullOrEmpty(ShipTypeName) || !string.IsNullOrEmpty(ShipName);
        HasLocation = !string.IsNullOrEmpty(LocationName) || !string.IsNullOrEmpty(SystemName);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasShipOrLocation)));
    }

    private void ApplySkillQueue(List<SkillQueueView>? queue)
    {
        SkillQueue.Clear();
        if (queue is not null)
        {
            foreach (var item in queue)
            {
                SkillQueue.Add(CreateQueueItem(item));
            }
        }

        HasSkillQueue = SkillQueue.Count > 0;
    }

    private static OverviewSkillQueueItemViewModel CreateQueueItem(SkillQueueView view)
    {
        var start = ToUtc(view.Start);
        var finish = ToUtc(view.Finish);
        var now = DateTime.UtcNow;

        // 与 Core.Models.Character.SkillQueueItem 的判定保持一致。
        var isPause = start is null || finish is null;
        var isFinished = !isPause && finish!.Value < now;
        var isWaiting = !isPause && start!.Value > now;
        var isRunning = !isPause && !isFinished && !isWaiting;

        var status = isPause
            ? OverviewSkillStatus.Pause
            : isFinished
                ? OverviewSkillStatus.Finished
                : isWaiting
                    ? OverviewSkillStatus.Waiting
                    : OverviewSkillStatus.Running;

        var remain = string.Empty;
        if (finish is not null)
        {
            var span = isRunning ? finish.Value - now : finish.Value - (start ?? finish.Value);
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }

            remain = span.Days >= 1
                ? $"{span.Days}d {span.Hours}h {span.Minutes}min"
                : $"{span.Hours}h {span.Minutes}min";
        }

        return new OverviewSkillQueueItemViewModel
        {
            SkillName = view.SkillName,
            FinishedLevel = view.FinishedLevel,
            Status = status,
            RemainTime = remain,
            StartText = start?.ToString("yyyy.MM.dd HH:mm") ?? string.Empty,
            FinishText = finish?.ToString("yyyy.MM.dd HH:mm") ?? string.Empty,
            StartTooltip = start?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
            FinishTooltip = finish?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
        };
    }

    private async Task LoadCorporationLogoAsync(long corporationId)
    {
        CorporationLogo = await LoadImageAsync(BuildImageUrl(corporationId, ImageKind.Corporation));
    }

    private async Task LoadAllianceLogoAsync(long allianceId)
    {
        AllianceLogo = await LoadImageAsync(BuildImageUrl(allianceId, ImageKind.Alliance));
    }

    private async Task LoadShipLogoAsync(long typeId)
    {
        ShipLogo = await LoadImageAsync(BuildImageUrl(typeId, ImageKind.Type));
    }

    /// <summary>下载并冻结图片：先在后台线程解码再 Freeze，避免跨线程访问 WIC 抛异常。</summary>
    private static async Task<ImageSource?> LoadImageAsync(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // 图片下载失败时保持为空，界面只显示底色/文字。
            return null;
        }
    }

    private enum ImageKind
    {
        Corporation,
        Alliance,
        Type,
    }

    /// <summary>图片地址与 WinUI <c>GameImageConverter</c> 规则一致（含静服域名）。</summary>
    private static string? BuildImageUrl(long id, ImageKind kind, int size = 64)
    {
        if (id <= 0)
        {
            return null;
        }

        if (GameServerSelectorService.Value == GameServerType.Serenity)
        {
            var segment = kind switch
            {
                ImageKind.Corporation => "corporations",
                ImageKind.Alliance => "alliances",
                _ => "types",
            };
            return $"https://image.evepc.163.com/{segment}/{id}_{size}.png";
        }

        return kind switch
        {
            ImageKind.Corporation => $"https://images.evetech.net/corporations/{id}/logo?size={size}",
            ImageKind.Alliance => $"https://images.evetech.net/alliances/{id}/logo?size={size}",
            _ => BuildTypeImageUrl(id, size),
        };
    }

    /// <summary>舰船 / 无人机用 render，其它物品用 icon（与 WinUI 一致）。</summary>
    private static string BuildTypeImageUrl(long typeId, int size)
    {
        try
        {
            var rootGroup = Core.Services.DB.InvMarketGroupService.QueryRootGroupOfType(typeId);
            if (rootGroup is 4 or 157)
            {
                return $"https://images.evetech.net/types/{typeId}/render?size={size}";
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return $"https://images.evetech.net/types/{typeId}/icon?size={size}";
    }

    /// <summary>ESI 时间统一按 UTC 处理。</summary>
    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
    }

    /// <summary>时长格式与 WinUI 卡片一致（y/mo/d/h/min 逐级降档）。</summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return "0min";
        }

        if (duration.TotalDays > 365)
        {
            return $"{duration.TotalDays / 365:N0}y {duration.TotalDays % 365 / 30:N1}mo";
        }

        if (duration.TotalDays > 30)
        {
            return $"{duration.TotalDays / 30:N0}mo {duration.TotalDays % 30:N1}d";
        }

        if (duration.TotalDays > 1)
        {
            return $"{duration.Days}d {duration.Hours + duration.Minutes / 60.0:N1}h";
        }

        return $"{duration.Hours}h {duration.Minutes:N0}min";
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
