using TheGuideToTheNewEden.WPF.Services.ChannelIntel;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;

/// <summary>
/// 叠加层的**信息总线**（阶段 355）：把各 provider 产出的信息行合并成一份列表。
/// <para>
/// 目前只有"频道预警"一个 provider；后续加功能时在这里注册即可，渲染侧（<c>PreviewNameOverlayWindow</c>）
/// 与调度侧（<c>GamePreviewWindow</c>）都不用改。
/// </para>
/// </summary>
public sealed class PreviewOverlayHub
{
    public static PreviewOverlayHub Current { get; } = new();

    private readonly List<IPreviewOverlayProvider> _providers = [];

    private PreviewOverlayHub()
    {
        // 内置 provider：频道预警（最近 / 最新预警）
        Register(new ChannelIntelOverlayProvider());
    }

    /// <summary>任意 provider 报告"某角色的叠加内容变了"（参数为角色名，null = 全量）。</summary>
    public event Action<string?>? Changed;

    /// <summary>注册一个 provider（重复注册同一个实例会被忽略）。</summary>
    public void Register(IPreviewOverlayProvider provider)
    {
        if (provider is null || _providers.Contains(provider))
        {
            return;
        }

        _providers.Add(provider);
        provider.Changed += OnProviderChanged;
    }

    public void Unregister(IPreviewOverlayProvider provider)
    {
        if (provider is null || !_providers.Remove(provider))
        {
            return;
        }

        provider.Changed -= OnProviderChanged;
    }

    /// <summary>合并全部 provider 的信息行，按 <see cref="PreviewOverlayItem.Order"/> 升序。</summary>
    public IReadOnlyList<PreviewOverlayItem> GetItems(PreviewOverlayContext context)
    {
        if (_providers.Count == 0)
        {
            return [];
        }

        var items = new List<PreviewOverlayItem>();
        foreach (var provider in _providers)
        {
            try
            {
                var rows = provider.GetItems(context);
                if (rows is { Count: > 0 })
                {
                    items.AddRange(rows.Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Text)));
                }
            }
            catch (Exception ex)
            {
                // 单个 provider 出错不能把整条叠加层拖垮
                Core.Log.Error(ex);
            }
        }

        return items.Count > 1
            ? items.OrderBy(p => p.Order).ToList()
            : items;
    }

    private void OnProviderChanged(string? characterName) => Changed?.Invoke(characterName);
}
