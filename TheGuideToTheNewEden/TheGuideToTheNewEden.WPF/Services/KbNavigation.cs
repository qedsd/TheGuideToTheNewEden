using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.KB;
using ZKB.NET;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 让任意界面（角色工作区的 ZKB 卡片、击杀列表、统计页里的实体名…）都能
/// "跳到 ZKB 主页面并打开某个实体统计标签 / 某条 KB 详情标签"。
///
/// 与 <see cref="Navigation.NavigateToMarket"/> 同一思路：先记下请求并跳转，
/// 页面（可能是首次创建）在加载时 <see cref="Drain"/> 取走请求；页面已存在时由事件立即触发，
/// 两条路径都会 <see cref="Drain"/>，因此不会重复打开。
/// </summary>
public static class KbNavigation
{
    /// <summary>要打开的实体。</summary>
    public sealed record EntityRequest(EntityType EntityType, int Id, string? Title);

    private static EntityRequest? _pendingEntity;
    private static int _pendingKillmailId;

    /// <summary>请求打开实体统计标签（页面已存在时触发）。</summary>
    public static event Action? Requested;

    /// <summary>打开指定实体的统计标签。</summary>
    public static void OpenEntity(IdName idName)
    {
        if (idName is null || idName.Id <= 0)
        {
            return;
        }

        OpenEntity(idName.Id, idName.GetCategory(), idName.Name);
    }

    /// <summary>打开指定类别的实体统计标签。</summary>
    public static void OpenEntity(int id, IdName.CategoryEnum category, string? title = null)
    {
        if (id <= 0)
        {
            return;
        }

        if (!ZkbMapping.TryToEntityType(category, out var entityType))
        {
            PageNotifyService.Info(FindString("ZKBPage_UnsupportedEntity"));
            return;
        }

        _pendingEntity = new EntityRequest(entityType, id, title);
        _pendingKillmailId = 0;
        NavigateAndNotify();
    }

    /// <summary>打开指定 killmail 的详情标签。</summary>
    public static void OpenKillmail(long killmailId)
    {
        if (killmailId <= 0)
        {
            return;
        }

        _pendingKillmailId = (int)killmailId;
        _pendingEntity = null;
        NavigateAndNotify();
    }

    /// <summary>取走待处理的请求（页面加载完成 / 收到事件时调用；取到即清空）。</summary>
    public static (EntityRequest? Entity, int KillmailId) Drain()
    {
        var entity = _pendingEntity;
        var killmailId = _pendingKillmailId;
        _pendingEntity = null;
        _pendingKillmailId = 0;
        return (entity, killmailId);
    }

    private static void NavigateAndNotify()
    {
        Navigation.Navigate(typeof(Views.Pages.ZKBPage));
        Requested?.Invoke();
        Navigation.Activate();
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
