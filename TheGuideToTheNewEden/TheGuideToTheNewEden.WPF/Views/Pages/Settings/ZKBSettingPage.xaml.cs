using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>ZKB 实时流设置：连接、通知与过滤条件（ID 列表为逗号分隔）。</summary>
public partial class ZKBSettingPage : Page
{
    private bool _loading = true;

    public ZKBSettingPage()
    {
        InitializeComponent();

        var setting = ZKBSettingService.Setting;

        AutoConnectToggle.IsChecked = setting.AutoConnect;
        NotifyToggle.IsChecked = setting.Notify;
        MaxKBItemsBox.Value = setting.MaxKBItems;
        MinNotifyValueBox.Value = setting.MinNotifyValue;

        TypesBox.Text = Join(setting.Types);
        SystemsBox.Text = Join(setting.Systems);
        RegionsBox.Text = Join(setting.Regions);
        CharactersBox.Text = Join(setting.Characters);
        CorpsBox.Text = Join(setting.Corps);
        AlliancesBox.Text = Join(setting.Alliances);

        AutoConnectToggle.Checked += (_, _) => Save();
        AutoConnectToggle.Unchecked += (_, _) => Save();
        NotifyToggle.Checked += (_, _) => Save();
        NotifyToggle.Unchecked += (_, _) => Save();
        MaxKBItemsBox.ValueChanged += (_, _) => Save();
        MinNotifyValueBox.ValueChanged += (_, _) => Save();
        TypesBox.LostFocus += (_, _) => Save();
        SystemsBox.LostFocus += (_, _) => Save();
        RegionsBox.LostFocus += (_, _) => Save();
        CharactersBox.LostFocus += (_, _) => Save();
        CorpsBox.LostFocus += (_, _) => Save();
        AlliancesBox.LostFocus += (_, _) => Save();

        _loading = false;
    }

    private static string Join(HashSet<int>? values)
    {
        return values is null || values.Count == 0 ? string.Empty : string.Join(",", values);
    }

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        try
        {
            var setting = ZKBSettingService.Setting;
            setting.AutoConnect = AutoConnectToggle.IsChecked == true;
            setting.Notify = NotifyToggle.IsChecked == true;
            setting.MaxKBItems = (int)(MaxKBItemsBox.Value ?? 0);
            setting.MinNotifyValue = (long)(MinNotifyValueBox.Value ?? 0);
            setting.Types = Parse(TypesBox.Text);
            setting.Systems = Parse(SystemsBox.Text);
            setting.Regions = Parse(RegionsBox.Text);
            setting.Characters = Parse(CharactersBox.Text);
            setting.Corps = Parse(CorpsBox.Text);
            setting.Alliances = Parse(AlliancesBox.Text);
            ZKBSettingService.Save();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            MessageBox.Show(
                ex.Message,
                Application.Current.TryFindResource("SettingPage_ZKB") as string ?? "ZKB",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static HashSet<int>? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToHashSet();
    }
}