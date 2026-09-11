using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 邮件详情窗口：WPF 原生头部信息（发件人/收件人/日期/标签）+ HtmlRenderer 渲染的 HTML 正文
/// （不使用 WebView2）。打开成功后按 WinUI 版行为将邮件标记为已读。
/// </summary>
public partial class MailDetailWindow : Wpf.Ui.Controls.FluentWindow
{
    private static readonly HttpClient Http = new();

    private readonly CharacterContext _context;
    private readonly long _mailId;
    private readonly bool _isUnread;

    /// <summary>详情成功加载并把邮件标记为已读后触发（供邮件列表刷新未读标记）。</summary>
    public event EventHandler? MarkedRead;

    public MailDetailWindow(CharacterContext context, long mailId, bool isUnread = false)
    {
        InitializeComponent();

        _context = context;
        _mailId = mailId;
        _isUnread = isUnread;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var mail = await CharacterMailService.GetMailAsync(_context, _mailId);
        if (mail is null)
        {
            SubjectText.Text = "-";
            return;
        }

        Title = mail.Subject;
        SubjectText.Text = mail.Subject;
        FromText.Text = mail.FromName;
        DateText.Text = mail.Date == DateTime.MinValue
            ? "-"
            : mail.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        RecipientsText.Text = mail.Recipients;
        LabelsText.Text = mail.Labels;
        FromInitial.Text = string.IsNullOrWhiteSpace(mail.FromName) ? "?" : mail.FromName[..1].ToUpperInvariant();

        BodyPanel.Text = BuildHtml(mail.BodyHtml);

        // 头像为可选信息，失败时保留首字母占位，不影响正文显示。
        if (mail.FromId > 0)
        {
            var avatar = await DownloadAvatarAsync(mail);
            if (avatar is not null)
            {
                FromAvatarBrush.ImageSource = avatar;
            }
        }

        // 与 WinUI 版一致：详情成功加载后标记已读。
        if (_isUnread && await CharacterMailService.MarkReadAsync(_context, _mailId))
        {
            MarkedRead?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>按发件人类别下载头像（evetech 图片服务）；无类别或失败返回 null。</summary>
    private static async Task<BitmapSource?> DownloadAvatarAsync(MailDetailView mail)
    {
        var url = mail.FromCategory switch
        {
            "character" => $"https://images.evetech.net/characters/{mail.FromId}/portrait?size=64",
            "corporation" => $"https://images.evetech.net/corporations/{mail.FromId}/logo?size=64",
            "alliance" => $"https://images.evetech.net/alliances/{mail.FromId}/logo?size=64",
            _ => null,
        };

        if (url is null)
        {
            return null;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);

            // BitmapImage 冻结前有线程亲缘性：在同一线程解码并立即 Freeze，冻结后才能跨线程绑定。
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
            return null;
        }
    }

    /// <summary>
    /// 邮件正文是简单 HTML，这里做最小清理：
    /// 去掉固定像素字号、剥离远程图片（离线/隐私），并给出基础样式。
    /// WinUI 版需要手工做颜色 hex↔RGB 转换，这里由渲染器直接处理。
    /// </summary>
    private static string BuildHtml(string body)
    {
        var html = body ?? string.Empty;
        html = System.Text.RegularExpressions.Regex.Replace(
            html, "size\\s*=\\s*\"?\\d+\"?", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(
            html, "<img[^>]*>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return "<html><head><style>"
               + "body{font-family:'Segoe UI','Microsoft YaHei UI',sans-serif;font-size:13px;}"
               + "a{color:#0a84ff;}"
               + "</style></head><body>" + html + "</body></html>";
    }
}
