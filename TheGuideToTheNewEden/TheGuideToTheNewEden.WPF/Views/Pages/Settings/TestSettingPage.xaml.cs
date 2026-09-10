using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>测试页：系统通知、声音播放、HKCR 协议读写（用于诊断）。</summary>
public partial class TestSettingPage : Page
{
    private readonly MediaPlayer _player = new();
    private string? _soundFile;

    public TestSettingPage()
    {
        InitializeComponent();

        SendToastButton.Click += (_, _) => SendToast();
        CheckNotificationButton.Click += (_, _) => CheckNotificationSupport();
        PlaySoundButton.Click += (_, _) => PlaySound();
        PauseSoundButton.Click += (_, _) => _player.Pause();
        PickSoundButton.Click += (_, _) => PickSound();

        ReadProtocolButton.Click += (_, _) => RunProtocolAction(
            () => ProtocolValueText.Text = AuthHelper.ReadProtocol() ?? string.Empty,
            "TestSettingPage_ReadProtocol_Success");
        WriteProtocolButton.Click += (_, _) => RunProtocolAction(
            AuthHelper.WriteProtocol,
            "TestSettingPage_RegistyProtocol_Success");
        DeleteProtocolButton.Click += (_, _) => RunProtocolAction(
            AuthHelper.DeleteProtocol,
            "TestSettingPage_DeleteProtocol_Success");
    }

    // ---------- 系统通知 ----------

    private void SendToast()
    {
        try
        {
            NotificationService.Show(
                FindString("SettingPage_Test"),
                FindString("TestSettingPage_SystemNotify_SendToast"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ShowDialog(FindString("TestSettingPage_SystemNotify"), ex.Message);
        }
    }

    private void CheckNotificationSupport()
    {
        // 使用托盘图标气泡通知，无需额外的系统注册。
        var status = NotificationService.IsAvailable
            ? FindString("TestSettingPage_SystemNotify_Setting_Enabled")
            : FindString("TestSettingPage_SystemNotify_Setting_Unsupported");

        ShowDialog(FindString("TestSettingPage_SystemNotify_CheckSettingResult"), status);
    }

    // ---------- 声音 ----------

    private void PlaySound()
    {
        try
        {
            var file = _soundFile;
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "default.mp3");
            }

            _player.Open(new Uri(file, UriKind.Absolute));
            _player.Play();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ShowDialog(FindString("TestSettingPage_MediaPlayer"), ex.Message);
        }
    }

    private void PickSound()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio|*.mp3;*.wav;*.wma|All files|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            _soundFile = dialog.FileName;
            ShowDialog(FindString("TestSettingPage_MediaPlayer"), dialog.FileName);
        }
    }

    // ---------- HKCR 协议 ----------

    private void RunProtocolAction(Action action, string successKey)
    {
        try
        {
            action();
            ProtocolValueText.Text = $"{FindString(successKey)}{Environment.NewLine}{ProtocolValueText.Text}";
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            ShowDialog(FindString("TestSettingPage_HKCRProtocol"), ex.Message);
        }
    }

    private static void ShowDialog(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private static string FindString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }
}