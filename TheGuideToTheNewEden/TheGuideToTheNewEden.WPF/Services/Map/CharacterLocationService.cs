using System.Windows;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>
/// 星图"显示角色"数据源：轮询所有已授权角色的 ESI 实时位置（Location.GetCharacterLocation），
/// 失败/无令牌的角色跳过。事件在后台线程触发，消费方自行调度 UI。
/// </summary>
public sealed class CharacterLocationService
{
    public static CharacterLocationService Current { get; } = new();

    private System.Timers.Timer? _timer;
    private int _polling;

    private CharacterLocationService()
    {
    }

    public sealed record CharacterLocation(AuthorizedCharacterData Character, int SystemId);

    /// <summary>一轮位置更新完成（整表替换语义）。</summary>
    public event Action<object?, IReadOnlyList<CharacterLocation>>? LocationsUpdated;

    public bool IsRunning { get; private set; }

    public void Start(int intervalMs = 30000)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _timer = new System.Timers.Timer(intervalMs)
        {
            AutoReset = false,
        };
        _timer.Elapsed += async (_, _) => await PollAsync();
        _ = PollAsync();
    }

    public void Stop()
    {
        IsRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>最近一轮的位置结果（从未轮询过则为空）。给"我在哪"这类只读查询用。</summary>
    public IReadOnlyList<CharacterLocation> LastLocations { get; private set; } = [];

    /// <summary>
    /// 立刻查一轮（**不启动轮询**、不触发 <see cref="LocationsUpdated"/>），并更新 <see cref="LastLocations"/>。
    /// 供 AI 助手的一次性查询使用；与轮询共用同一段取数逻辑。
    /// </summary>
    public async Task<IReadOnlyList<CharacterLocation>> QueryOnceAsync(CancellationToken ct = default)
    {
        var locations = await CollectAsync(ct);
        LastLocations = locations;
        return locations;
    }

    private async Task PollAsync()
    {
        if (!IsRunning || Interlocked.Exchange(ref _polling, 1) == 1)
        {
            return;
        }

        try
        {
            var locations = await CollectAsync(CancellationToken.None);
            LastLocations = locations;
            LocationsUpdated?.Invoke(this, locations);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
            if (IsRunning)
            {
                _timer?.Start();
            }
        }
    }

    /// <summary>取一轮所有已授权角色的实时位置（失败/无令牌的角色跳过）。</summary>
    private static async Task<IReadOnlyList<CharacterLocation>> CollectAsync(CancellationToken ct)
    {
        var characters = new List<AuthorizedCharacterData>();
        foreach (var character in CharacterStore.Characters)
        {
            ct.ThrowIfCancellationRequested();
            if (character.IsTokenValid() || await CharacterStore.EnsureTokenValidAsync(character))
            {
                characters.Add(character);
            }
        }

        var locations = new List<CharacterLocation>(characters.Count);
        foreach (var character in characters)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var esi = Core.Services.ESIService.GetDefaultESI();
                var auth = Core.Services.ESIService.ToEVEStandardSSO(character);
                var resp = await esi.Location.GetCharacterLocationAsync(auth);
                if (resp?.Model is not null)
                {
                    locations.Add(new CharacterLocation(character, (int)resp.Model.SolarSystemId));
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error($"星图获取角色 {character.CharacterName} 位置失败：{ex.Message}");
            }
        }

        return locations;
    }
}
