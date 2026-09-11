using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>角色卡片（也用于"添加角色"占位卡片）。字段与 WinUI3 卡片一致。</summary>
public sealed class CharacterCardViewModel : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    private ImageSource? _avatar;
    private bool _online;
    private string _offlineTimeText = string.Empty;
    private long _skillPoints;
    private double _wallet;
    private long _loyalty;
    private int _queueTotalCount;
    private int _queueUndoneCount;
    private bool _queueRunning;
    private double _queuePercent;
    private string _queuePercentText = "0";
    private string _queueRemainText = string.Empty;
    private bool _loaded;

    public required AuthorizedCharacterData Character { get; init; }

    /// <summary>true 表示这是末尾的"添加角色"卡片。</summary>
    public bool IsAdd { get; init; }

    public long CharacterId => Character.CharacterID;

    public string Name => Character.CharacterName;

    /// <summary>头像加载失败时显示的占位首字母。</summary>
    public string Initial =>
        string.IsNullOrWhiteSpace(Character.CharacterName) ? "?" : Character.CharacterName[..1].ToUpperInvariant();

    public ImageSource? Avatar
    {
        get => _avatar;
        private set => Set(ref _avatar, value);
    }

    public bool Online
    {
        get => _online;
        private set => Set(ref _online, value);
    }

    /// <summary>离线时长（如 "2d 3.5h"），与 WinUI 卡片的 OffLineTime 同款格式。</summary>
    public string OfflineTimeText
    {
        get => _offlineTimeText;
        private set => Set(ref _offlineTimeText, value);
    }

    public long SkillPoints
    {
        get => _skillPoints;
        private set => Set(ref _skillPoints, value);
    }

    public double Wallet
    {
        get => _wallet;
        private set => Set(ref _wallet, value);
    }

    public long Loyalty
    {
        get => _loyalty;
        private set => Set(ref _loyalty, value);
    }

    public bool Loaded
    {
        get => _loaded;
        private set => Set(ref _loaded, value);
    }

    public string SkillPointsText => _skillPoints > 0 ? _skillPoints.ToString("N0") : "-";

    public string WalletText => _wallet > 0 ? FormatIsk(_wallet) : "-";

    /// <summary>技能队列总条数。</summary>
    public int QueueTotalCount
    {
        get => _queueTotalCount;
        private set => Set(ref _queueTotalCount, value);
    }

    /// <summary>技能队列中尚未完成的条数。</summary>
    public int QueueUndoneCount
    {
        get => _queueUndoneCount;
        private set => Set(ref _queueUndoneCount, value);
    }

    /// <summary>技能队列是否正在训练（WinUI 用"是否有且仅有一条处于训练中"判定）。</summary>
    public bool QueueRunning
    {
        get => _queueRunning;
        private set => Set(ref _queueRunning, value);
    }

    /// <summary>技能队列剩余百分比（0~100，与 WinUI 的 SkillQueueRemainRatio 同义）。</summary>
    public double QueuePercent
    {
        get => _queuePercent;
        private set => Set(ref _queuePercent, value);
    }

    public string QueuePercentText
    {
        get => _queuePercentText;
        private set => Set(ref _queuePercentText, value);
    }

    /// <summary>队列剩余时间文本；未在训练时为空。</summary>
    public string QueueRemainText
    {
        get => _queueRemainText;
        private set => Set(ref _queueRemainText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>加载卡片数据（总览 + 技能）。失败时保持占位显示。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (IsAdd)
        {
            return;
        }

        await LoadAvatarAsync();

        var context = new Services.Characters.CharacterContext(Character);

        var overview = await Services.Characters.CharacterOverviewService.GetAsync(context, forceRefresh);
        if (overview is not null)
        {
            Online = overview.Online;
            Wallet = overview.WalletBalance;
            Loyalty = overview.LoyaltyPoints;
            OfflineTimeText = overview.Online ? string.Empty : FormatOfflineTime(overview.LastLogout);
        }

        var skills = await Services.Characters.CharacterSkillService.GetAsync(context, forceRefresh);
        if (skills is not null)
        {
            SkillPoints = skills.TotalSkillPoints;
            ApplyQueue(skills.Queue);
        }

        Loaded = true;
        RaiseAll();
    }

    private async Task LoadAvatarAsync()
    {
        try
        {
            Avatar = await DownloadAvatarAsync(CharacterId);
        }
        catch
        {
            // 头像下载失败（离线/网络受限）时保持为空，界面显示占位首字母。
            Avatar = null;
        }
    }

    /// <summary>
    /// 下载并解码头像。下载完成后在线程池线程上解码 + 冻结，不阻塞 UI。
    /// </summary>
    /// <remarks>
    /// <see cref="BitmapImage"/> 是 <see cref="System.Windows.Freezable"/>：冻结前有线程亲缘性，
    /// <c>Freeze()</c> 必须与创建在同一线程，否则访问内部的 WIC 状态（如 <c>IsDownloading</c>）会抛
    /// "调用线程无法访问此对象"。这里刻意先把字节抓下来，再用 <see cref="MemoryStream"/> 在同一线程
    /// 解码并立即冻结，冻结后即可安全地跨线程交给绑定。
    /// </remarks>
    private static async Task<BitmapSource> DownloadAvatarAsync(long characterId)
    {
        var bytes = await Http
            .GetByteArrayAsync($"https://images.evetech.net/characters/{characterId}/portrait?size=128")
            .ConfigureAwait(false);

        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 技能队列统计。与 WinUI 一致：队列要么全在训练、要么全暂停，因此任一条处于训练中即视为在训练；
    /// 剩余百分比按"剩余时间 / 队列总时长"计算。
    /// </summary>
    private void ApplyQueue(List<Services.Characters.SkillQueueView> queue)
    {
        QueueTotalCount = queue.Count;
        if (queue.Count == 0)
        {
            QueueUndoneCount = 0;
            QueueRunning = false;
            QueuePercent = 0;
            QueuePercentText = "0";
            QueueRemainText = string.Empty;
            return;
        }

        var now = DateTime.Now;
        var starts = queue.Where(p => p.Start is not null).Select(p => Localize(p.Start!.Value)).ToList();
        var finishes = queue.Where(p => p.Finish is not null).Select(p => Localize(p.Finish!.Value)).ToList();

        // "已完成"以结束时间为准；无结束时间的一律算未完成。
        QueueUndoneCount = queue.Count(p => p.Finish is null || Localize(p.Finish.Value) >= now);
        QueueRunning = queue.Any(p => p.Start is not null && p.Finish is not null
                                      && Localize(p.Start.Value) <= now && Localize(p.Finish.Value) > now);

        if (finishes.Count == 0)
        {
            QueuePercent = 0;
            QueuePercentText = "0";
            QueueRemainText = string.Empty;
            return;
        }

        var lastFinish = finishes.Max();
        var remain = lastFinish - now;
        if (remain <= TimeSpan.Zero)
        {
            QueuePercent = 0;
            QueuePercentText = "0";
            QueueRemainText = string.Empty;
            return;
        }

        QueueRemainText = $"{remain.Days}d {remain.Hours}h {remain.Minutes}min";

        if (starts.Count > 0 && QueueRunning)
        {
            var total = lastFinish - starts.Min();
            QueuePercent = total > TimeSpan.Zero
                ? Math.Clamp(remain / total * 100, 0, 100)
                : 0;
            QueuePercentText = QueuePercent.ToString("N0");
        }
        else
        {
            QueuePercent = 0;
            QueuePercentText = "0";
        }
    }

    /// <summary>ESI 时间统一转成本地时间后再参与比较与显示。</summary>
    private static DateTime Localize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Local => value,
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
    };

    /// <summary>离线时长，格式与 WinUI 卡片一致（y/mo/d/h/min 逐级降档）。</summary>
    private static string FormatOfflineTime(DateTime? lastLogout)
    {
        if (lastLogout is null)
        {
            return string.Empty;
        }

        var offline = DateTime.Now - Localize(lastLogout.Value);
        if (offline <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        if (offline.TotalDays > 365)
        {
            return $"{offline.TotalDays / 365:N0}y {offline.TotalDays % 365 / 30:N1}mo";
        }

        if (offline.TotalDays > 30)
        {
            return $"{offline.TotalDays / 30:N0}mo {offline.TotalDays % 30:N1}d";
        }

        if (offline.TotalDays > 1)
        {
            return $"{offline.Days}d {offline.Hours + offline.Minutes / 60.0:N1}h";
        }

        return $"{offline.Hours}h {offline.Minutes:N0}min";
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Online), nameof(OfflineTimeText), nameof(Wallet), nameof(Loyalty), nameof(SkillPoints),
                     nameof(SkillPointsText), nameof(WalletText),
                     nameof(QueueTotalCount), nameof(QueueUndoneCount), nameof(QueueRunning),
                     nameof(QueuePercent), nameof(QueuePercentText), nameof(QueueRemainText),
                     nameof(Loaded),
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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

    internal static string FormatIsk(double value)
    {
        return value switch
        {
            >= 1_000_000_000_000 => $"{value / 1_000_000_000_000:0.##}T ISK",
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B ISK",
            >= 1_000_000 => $"{value / 1_000_000:0.##}M ISK",
            >= 1_000 => $"{value / 1_000:0.##}K ISK",
            _ => $"{value:0.##} ISK",
        };
    }

    private static string Resolve(string key) => Application.Current?.TryFindResource(key) as string ?? key;
}
