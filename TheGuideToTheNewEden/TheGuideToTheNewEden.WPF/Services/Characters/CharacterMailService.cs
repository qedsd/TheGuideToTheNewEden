using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

public sealed class MailLabelView
{
    public long LabelId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int UnreadCount { get; set; }
}

public sealed class MailHeaderView
{
    public long MailId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public bool IsRead { get; set; }
}

public sealed class MailDetailView
{
    public string Subject { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    /// <summary>发件人 ID（用于加载头像）。</summary>
    public long FromId { get; set; }

    /// <summary>发件人类别（character / corporation / alliance，用于拼接头像地址）。</summary>
    public string? FromCategory { get; set; }

    public DateTime Date { get; set; }

    public string Recipients { get; set; } = string.Empty;

    public string Labels { get; set; } = string.Empty;

    /// <summary>邮件正文（HTML）。</summary>
    public string BodyHtml { get; set; } = string.Empty;
}

/// <summary>角色邮件：标签、列表、正文与已读标记。</summary>
public static class CharacterMailService
{
    private const string LabelCacheKey = "mail.labels";

    /// <summary>标签与未读数缓存 2 分钟。</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static async Task<List<MailLabelView>?> GetLabelsAsync(CharacterContext context, bool forceRefresh = false)
    {
        if (!forceRefresh && CharacterCache.TryGet<List<MailLabelView>>(context.CharacterId, LabelCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var labels = (await context.Api.Mail.GetMailLabelsAndUnreadCountsAsync(context.Auth)).Model;
            var views = (labels?.Labels ?? []).Select(label => new MailLabelView
            {
                LabelId = label.LabelId ?? 0,
                Name = ResolveLabelName(label.Name),
                UnreadCount = (int)(label.UnreadCount ?? 0),
            }).ToList();

            CharacterCache.Set(context.CharacterId, LabelCacheKey, views, Ttl);
            return views;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    public static async Task<List<MailHeaderView>?> GetHeadersAsync(CharacterContext context, long? labelId)
    {
        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var labelIds = labelId.HasValue ? new List<long> { labelId.Value } : new List<long>();
            var headers = (await context.Api.Mail.ReturnMailHeadersAsync(context.Auth, labelIds, 0L)).Model ?? [];

            var senderIds = headers.Select(p => p.From ?? 0).Where(p => p > 0).Distinct().ToList();
            var senderNames = await ResolveNamesAsync(senderIds);

            return headers.Select(header => new MailHeaderView
            {
                MailId = header.MailId ?? 0,
                Subject = string.IsNullOrWhiteSpace(header.Subject) ? "(no subject)" : header.Subject,
                FromName = senderNames.TryGetValue(header.From ?? 0, out var fromName)
                    ? fromName.Name
                    : header.From?.ToString() ?? "-",
                Date = header.Timestamp ?? DateTime.MinValue,
                IsRead = header.IsRead ?? false,
            }).ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    public static async Task<MailDetailView?> GetMailAsync(CharacterContext context, long mailId)
    {
        if (!await context.EnsureTokenValidAsync())
        {
            return null;
        }

        try
        {
            var mail = (await context.Api.Mail.ReturnMailAsync(context.Auth, mailId)).Model;
            if (mail is null)
            {
                return null;
            }

            var fromId = mail.From ?? 0;
            var recipientIds = (mail.Recipients ?? [])
                .Select(r => r.RecipientId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var ids = new List<long>(recipientIds);
            if (fromId > 0)
            {
                ids.Add(fromId);
            }

            var names = await ResolveNamesAsync(ids.Distinct().ToList());

            // 收件人显示名称（解析失败时退回 ID），与 WinUI 版一致用 ";" 连接。
            var recipientNames = recipientIds.Select(id =>
                names.TryGetValue(id, out var entity) ? entity.Name : id.ToString());

            // 标签名走标签缓存（与 MailPage 共用，通常命中缓存），ID 解析不到时忽略。
            var labelNames = new List<string>();
            var labelIds = (mail.Labels ?? []).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (labelIds.Count > 0)
            {
                var labels = await GetLabelsAsync(context);
                if (labels is not null)
                {
                    foreach (var labelId in labelIds)
                    {
                        var match = labels.FirstOrDefault(l => l.LabelId == labelId);
                        if (match is not null)
                        {
                            labelNames.Add(match.Name);
                        }
                    }
                }
            }

            return new MailDetailView
            {
                Subject = string.IsNullOrWhiteSpace(mail.Subject) ? "(no subject)" : mail.Subject,
                FromName = names.TryGetValue(fromId, out var from)
                    ? from.Name
                    : mail.From?.ToString() ?? "-",
                FromId = fromId,
                FromCategory = names.TryGetValue(fromId, out var fromEntity)
                    ? ToCategoryString(fromEntity.GetCategory())
                    : null,
                Date = mail.Timestamp ?? DateTime.MinValue,
                Recipients = string.Join("; ", recipientNames),
                Labels = string.Join("; ", labelNames),
                BodyHtml = mail.Body ?? string.Empty,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>标记邮件为已读（与 WinUI 版打开详情后调用的接口一致）。</summary>
    public static async Task<bool> MarkReadAsync(CharacterContext context, long mailId)
    {
        if (!await context.EnsureTokenValidAsync())
        {
            return false;
        }

        try
        {
            await context.Api.Mail.UpdateMetadataAboutMailAsync(
                context.Auth,
                mailId,
                new EVEStandard.Models.UpdateMailMetadata { Read = true });
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    /// <summary>标签名走本地化资源（与 WinUI 版一致）。</summary>
    private static string ResolveLabelName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "-";
        }

        return Application.Current?.TryFindResource(name) as string ?? name;
    }

    /// <summary>ID 类别转 ESI 的小写字符串（用于头像地址，如 character/corporation/alliance）。</summary>
    private static string ToCategoryString(IdName.CategoryEnum category) => category switch
    {
        IdName.CategoryEnum.Character => "character",
        IdName.CategoryEnum.Corporation => "corporation",
        IdName.CategoryEnum.Alliance => "alliance",
        _ => string.Empty,
    };

    private static async Task<Dictionary<long, IdName>> ResolveNamesAsync(List<long> ids)
    {
        var result = new Dictionary<long, IdName>();
        if (ids.Count == 0)
        {
            return result;
        }

        try
        {
            // 邮件的发件人/收件人只可能是角色、军团、联盟或邮件列表，均在 int 范围内，
            // 因此可直接用 IDNameService；结构 ID（约 1e12）请改用 LocationNameResolver。
            var names = await IDNameService.GetByIdsAsync(ids);
            foreach (var item in names ?? [])
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    result[item.Id] = item;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }
}