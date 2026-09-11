using System.Windows;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 国服（Serenity）授权向导：登出 → 打开授权页 → 粘贴网址 → 校验。
/// 国服没有可用的自定义协议回调，只能由用户把授权后空白页的网址（或 code）复制回来，
/// 步骤与提示沿用 WinUI 版 <c>AddSerenityAuthDialog</c>。
/// </summary>
public partial class SerenityAuthWindow : Wpf.Ui.Controls.FluentWindow
{
    public SerenityAuthWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CodeBox.Focus();
    }

    /// <summary>校验成功后的角色数据；未完成或失败时为 null。</summary>
    public AuthorizedCharacterData? Result { get; private set; }

    private void Logoff_Click(object sender, RoutedEventArgs e)
    {
        CharacterAuthService.OpenSerenityLogoffPage();
    }

    private void OpenAuthPage_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureSerenityServer())
        {
            return;
        }

        CharacterAuthService.OpenAuthorizationPage();
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        // 用户可能在向导打开期间去设置里把服务器切走了，这里再确认一次
        if (!EnsureSerenityServer())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            ShowFailed(FindString("Characters.SerenityPasteCode"));
            return;
        }

        SetBusy(true);
        try
        {
            var character = await CharacterAuthService.CompleteSerenityAsync(CodeBox.Text);
            if (character is null)
            {
                ShowFailed(DescribeLastError());
                return;
            }

            Result = character;
            HideStatus();
            SuccessText.Visibility = Visibility.Visible;
            VerifyButton.IsEnabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>国服授权必须建立在"当前服务器 = Serenity"之上（客户端 ID 与数据源都不同）。</summary>
    private bool EnsureSerenityServer()
    {
        if (CharacterAuthService.IsSerenity)
        {
            return true;
        }

        HideStatus();
        FailedDetail.Text = FindString("Characters.SerenityWrongServer");
        FailedDetail.Visibility = Visibility.Visible;
        FailedText.Visibility = Visibility.Visible;
        return false;
    }

    private void SetBusy(bool busy)
    {
        VerifyButton.IsEnabled = !busy;
        VerifyingText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            SuccessText.Visibility = Visibility.Collapsed;
            FailedText.Visibility = Visibility.Collapsed;
            FailedDetail.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowFailed(string? detail)
    {
        HideStatus();
        FailedText.Visibility = Visibility.Visible;
        FailedDetail.Text = detail ?? string.Empty;
        FailedDetail.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideStatus()
    {
        VerifyingText.Visibility = Visibility.Collapsed;
        SuccessText.Visibility = Visibility.Collapsed;
        FailedText.Visibility = Visibility.Collapsed;
        FailedDetail.Visibility = Visibility.Collapsed;
    }

    /// <summary>校验失败时把 Core 记下的最后一条错误显示出来（WinUI 版同样展示该异常信息）。</summary>
    private static string? DescribeLastError()
    {
        return Core.Log.GetLastError() switch
        {
            Exception ex => ex.Message,
            { } other => other.ToString(),
            _ => null,
        };
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
