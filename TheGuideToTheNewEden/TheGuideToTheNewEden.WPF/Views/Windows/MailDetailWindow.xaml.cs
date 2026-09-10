using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 邮件详情窗口：WPF 原生头部信息 + HtmlRenderer 渲染的 HTML 正文（不使用 WebView2）。
/// </summary>
public partial class MailDetailWindow : Window
{
    private readonly CharacterContext _context;
    private readonly long _mailId;

    public MailDetailWindow(CharacterContext context, long mailId)
    {
        InitializeComponent();

        _context = context;
        _mailId = mailId;

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
        FromText.Text = $"{FindString("Characters.Mail.From")}: {mail.FromName}";
        DateText.Text = mail.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        RecipientsText.Text = $"{FindString("Characters.Mail.Recipients")}: {mail.Recipients}";
        LabelsText.Text = $"{FindString("Characters.Mail.Labels")}: {mail.Labels}";

        BodyPanel.Text = BuildHtml(mail.BodyHtml);
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


    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}