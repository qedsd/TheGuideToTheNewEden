using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>游戏日志设置：日志目录、频道文件有效天数、显示条数。</summary>
public partial class GameLogSettingPage : Page
{
    public GameLogSettingPage()
    {
        InitializeComponent();

        LogsPathTextBox.Text = GameLogsSettingService.EVELogsPathValue;
        LogsPathTextBox.LostFocus += (_, _) =>
            GameLogsSettingService.SetValue(GameLogsSettingService.GameLogKey.EVELogsPath, LogsPathTextBox.Text);

        ChannelDurationBox.Value = GameLogsSettingService.EVELogsChannelDurationValue;
        ChannelDurationBox.ValueChanged += (_, _) =>
            GameLogsSettingService.SetValue(
                GameLogsSettingService.GameLogKey.EVELogsChannelDuration,
                ((int)ChannelDurationBox.Value).ToString());

        MaxShowItemsBox.Value = GameLogsSettingService.MaxShowItems;
        MaxShowItemsBox.ValueChanged += (_, _) =>
            GameLogsSettingService.SetValue(
                GameLogsSettingService.GameLogKey.MaxShowItems,
                ((int)MaxShowItemsBox.Value).ToString());
    }
}