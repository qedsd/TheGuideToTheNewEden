using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>角色卡片（也用于"添加角色"占位卡片）。</summary>
public sealed class CharacterCardViewModel : INotifyPropertyChanged
{
    private ImageSource? _avatar;
    private bool _online;
    private long _skillPoints;
    private double _wallet;
    private long _loyalty;
    private int _queueCount;
    private int _queueRemaining;
    private DateTime? _queueFinish;
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

    public string StatusText => Resolve(_online ? "Characters.Online" : "Characters.Offline");

    public string QueueText =>
        _queueCount == 0
            ? Resolve("Characters.NotTraining")
            : string.Format("{0} / {1}", _queueCount - _queueRemaining, _queueCount);

    public double QueueRatio =>
        _queueCount == 0 ? 0 : (_queueCount - _queueRemaining) / (double)_queueCount;

    public string QueueRemainText =>
        _queueFinish is null ? Resolve("Characters.NotTraining") : FormatRemain(_queueFinish.Value - DateTime.UtcNow);

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
        }

        var skills = await Services.Characters.CharacterSkillService.GetAsync(context, forceRefresh);
        if (skills is not null)
        {
            SkillPoints = skills.TotalSkillPoints;
            QueueCount = skills.Queue.Count;
            QueueRemaining = skills.Queue.Count(p => !p.IsRunning && (p.Finish is null || p.Finish > DateTime.UtcNow));
            _queueFinish = skills.Queue.Select(p => p.Finish).Where(p => p is not null).DefaultIfEmpty(null).Max();
        }

        Loaded = true;
        RaiseAll();
    }

    private int QueueCount
    {
        get => _queueCount;
        set => Set(ref _queueCount, value);
    }

    private int QueueRemaining
    {
        get => _queueRemaining;
        set => Set(ref _queueRemaining, value);
    }

    private async Task LoadAvatarAsync()
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri($"https://images.evetech.net/characters/{CharacterId}/portrait?size=128");
            bitmap.EndInit();

            await Task.Run(() => bitmap.Freeze());
            Avatar = bitmap;
        }
        catch
        {
            // 头像下载失败（离线/网络受限）时保持为空，界面显示占位。
            Avatar = null;
        }
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Online), nameof(Wallet), nameof(Loyalty), nameof(SkillPoints),
                     nameof(SkillPointsText), nameof(WalletText), nameof(StatusText),
                     nameof(QueueText), nameof(QueueRatio), nameof(QueueRemainText), nameof(Loaded),
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

    internal static string FormatRemain(TimeSpan remain)
    {
        if (remain <= TimeSpan.Zero)
        {
            return "-";
        }

        if (remain.TotalDays >= 1)
        {
            return $"{(int)remain.TotalDays}d {remain.Hours}h";
        }

        return remain.TotalHours >= 1 ? $"{(int)remain.TotalHours}h {remain.Minutes}m" : $"{remain.Minutes}m";
    }

    private static string Resolve(string key) => Application.Current?.TryFindResource(key) as string ?? key;
}