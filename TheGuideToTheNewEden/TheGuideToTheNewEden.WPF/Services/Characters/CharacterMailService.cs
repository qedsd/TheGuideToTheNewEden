using System.Windows;
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
                    ? fromName
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

            var ids = new List<long>();
            if (mail.From.HasValue)
            {
                ids.Add(mail.From.Value);
            }

            if (mail.Recipients is not null)
            {
                // Recipients 的具体类型在不同 EVEStandard 版本间有差异，这里只取收件人数，
                // 名称解析留待确认模型后补齐（避免破坏编译）。
                foreach (var recipientId in mail.Recipients.Select(r => Convert.ToInt64(r.RecipientId)))
                {
                    ids.Add(recipientId);
                }
            }

            var names = await ResolveNamesAsync(ids.Distinct().ToList());

            return new MailDetailView
            {
                Subject = string.IsNullOrWhiteSpace(mail.Subject) ? "(no subject)" : mail.Subject,
                FromName = names.TryGetValue(mail.From ?? 0, out var fromName)
                    ? fromName
                    : mail.From?.ToString() ?? "-",
                Date = mail.Timestamp ?? DateTime.MinValue,
                Recipients = string.Join(", ", ids.Skip(1).Select(id => id.ToString())),
                // Labels 的元素类型随 EVEStandard 版本变化，暂不映射（正文/发件人/时间已足够）
                Labels = string.Empty,
                BodyHtml = mail.Body ?? string.Empty,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
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

    private static async Task<Dictionary<long, string>> ResolveNamesAsync(List<long> ids)
    {
        var result = new Dictionary<long, string>();
        if (ids.Count == 0)
        {
            return result;
        }

        try
        {
            var names = await IDNameService.GetByIdsAsync(ids);
            foreach (var item in names ?? [])
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    result[item.Id] = item.Name;
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