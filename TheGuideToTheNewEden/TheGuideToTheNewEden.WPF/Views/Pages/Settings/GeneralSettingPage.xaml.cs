using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>常规设置：语言、主题、主题色、托盘、本地化数据库、游戏服务器、自动更新、打开目录。</summary>
public partial class GeneralSettingPage : Page
{
    private readonly Dictionary<Color, SymbolIcon> _accentCheckmarks = new();

    public GeneralSettingPage()
    {
        InitializeComponent();

        BuildAccentSwatches();
        InitializeLanguageComboBox();
        InitializeThemeComboBox();
        InitializeTrayToggle();
        InitializeLocalDb();
        InitializeGameServer();
        InitializeAutoUpdate();
        InitializeOpenButtons();
        InitializeDataPath();
    }

    // ---------- 主题色 ----------

    private void BuildAccentSwatches()
    {
        AccentSwatchPanel.Children.Clear();
        _accentCheckmarks.Clear();

        foreach (var color in ThemeService.AccentColors)
        {
            var circle = new Ellipse
            {
                Width = 28,
                Height = 28,
                Fill = new SolidColorBrush(color),
            };

            var checkmark = new SymbolIcon
            {
                Symbol = SymbolRegular.Checkmark24,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(GetContrastColor(color)),
                Visibility = Visibility.Collapsed,
            };

            var cell = new Grid();
            cell.Children.Add(circle);
            cell.Children.Add(checkmark);

            var swatch = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Transparent,
                Padding = new Thickness(0),
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Tag = color,
                ToolTip = ThemeService.GetAccentName(color),
                Content = cell,
            };
            swatch.Click += AccentColor_Click;

            AccentSwatchPanel.Children.Add(swatch);
            _accentCheckmarks[color] = checkmark;
        }

        RefreshAccentSelection();
    }

    private void AccentColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Color color })
        {
            ThemeService.ApplyAccentColor(color);
            RefreshAccentSelection();
            AccentToggle.IsChecked = false;
        }
    }

    private void RefreshAccentSelection()
    {
        var current = ThemeService.AccentColor;
        AccentSwatch.Background = new SolidColorBrush(current);

        foreach (var (color, checkmark) in _accentCheckmarks)
        {
            checkmark.Visibility = color == current ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static Color GetContrastColor(Color background)
    {
        var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
        return luminance > 0.55 ? Colors.Black : Colors.White;
    }

    // ---------- 语言 ----------

    private void InitializeLanguageComboBox()
    {
        var zh = new ComboBoxItem { Content = "中文", Tag = "zh-CN" };
        var en = new ComboBoxItem { Content = "English", Tag = "en-US" };
        LanguageComboBox.Items.Add(zh);
        LanguageComboBox.Items.Add(en);

        LanguageComboBox.SelectedItem = LanguageService.Value == "en-US" ? en : zh;
        LanguageComboBox.SelectionChanged += (_, _) =>
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
            {
                LanguageService.SetLanguage(tag);
            }
        };
    }

    // ---------- 主题 ----------

    private void InitializeThemeComboBox()
    {
        var light = new ComboBoxItem { Content = FindString("Setting_Theme_Light"), Tag = false };
        var dark = new ComboBoxItem { Content = FindString("Setting_Theme_Dark"), Tag = true };
        ThemeComboBox.Items.Add(light);
        ThemeComboBox.Items.Add(dark);

        ThemeComboBox.SelectedItem = ThemeService.Theme == ApplicationTheme.Dark ? dark : light;
        ThemeComboBox.SelectionChanged += (_, _) =>
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem { Tag: bool isDark })
            {
                ThemeService.ApplyTheme(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            }
        };
    }

    // ---------- 托盘 ----------

    private void InitializeTrayToggle()
    {
        MinimizeToTrayToggle.IsChecked = SettingsService.GetBool(SettingsService.MinimizeToTrayKey);
        MinimizeToTrayToggle.Checked += (_, _) => SettingsService.SetBool(SettingsService.MinimizeToTrayKey, true);
        MinimizeToTrayToggle.Unchecked += (_, _) => SettingsService.SetBool(SettingsService.MinimizeToTrayKey, false);
    }

    // ---------- 本地化数据库 ----------

    private void InitializeLocalDb()
    {
        NeedLocalizationCheckBox.IsChecked = DBLocalizationSettingService.Value;
        NeedLocalizationCheckBox.Click += (_, _) =>
            DBLocalizationSettingService.Set(NeedLocalizationCheckBox.IsChecked == true);

        var files = LocalDbSelectorService.GetAll();
        foreach (var file in files)
        {
            LocalDbComboBox.Items.Add(new ComboBoxItem
            {
                Content = System.IO.Path.GetFileNameWithoutExtension(file),
                Tag = file,
            });
        }

        LocalDbComboBox.SelectedItem = LocalDbComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string?)i.Tag == LocalDbSelectorService.Value)
            ?? LocalDbComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();

        LocalDbComboBox.SelectionChanged += (_, _) =>
        {
            if (LocalDbComboBox.SelectedItem is ComboBoxItem { Tag: string path })
            {
                LocalDbSelectorService.Set(path);
            }
        };
    }

    // ---------- 游戏服务器 ----------

    private void InitializeGameServer()
    {
        var tq = new ComboBoxItem { Content = "Tranquility", Tag = GameServerType.Tranquility };
        var serenity = new ComboBoxItem { Content = "Serenity", Tag = GameServerType.Serenity };
        GameServerComboBox.Items.Add(tq);
        GameServerComboBox.Items.Add(serenity);

        GameServerComboBox.SelectedItem =
            GameServerSelectorService.Value == GameServerType.Serenity ? serenity : tq;

        GameServerComboBox.SelectionChanged += (_, _) =>
        {
            if (GameServerComboBox.SelectedItem is not ComboBoxItem { Tag: GameServerType server })
            {
                return;
            }

            if (server == GameServerSelectorService.Value)
            {
                return;
            }

            // 立即生效：重灌 ESI 凭据（国服用独立客户端 ID）+ 重建 ESI 单例 + 载入该国服的已授权角色。
            CoreInitializer.SwitchGameServer(server);
            System.Windows.MessageBox.Show(
                string.Format(FindString("Setting_GameServer_Switched_Description"), server),
                FindString("Setting_GameServer_Switched_Title"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        };
    }

    // ---------- 自动更新 ----------

    private void InitializeAutoUpdate()
    {
        AutoUpdateCheckBox.IsChecked = AutoUpdateService.Value;
        AutoUpdateCheckBox.Click += (_, _) => AutoUpdateService.Set(AutoUpdateCheckBox.IsChecked == true);
    }

    // ---------- 打开目录 ----------

    private void InitializeOpenButtons()
    {
        OpenLogButton.Click += (_, _) => ShellHelper.OpenFolder(Core.Log.GetLogPath());
    }

    // ---------- 数据目录（阶段 186） ----------

    private void InitializeDataPath()
    {
        RefreshDataPathText();

        OpenDataPathButton.Click += (_, _) => ShellHelper.OpenFolder(DataPathService.Current);
        ChangeDataPathButton.Click += (_, _) => PickDataPath();
        ResetDataPathButton.Click += (_, _) =>
        {
            if (!IsSameFolder(DataPathService.PendingDataPath, DataPathService.DefaultDataPath))
            {
                ApplyDataPath(DataPathService.DefaultDataPath);
            }
        };
    }

    private void RefreshDataPathText()
    {
        var pending = DataPathService.PendingDataPath;
        var isPending = !IsSameFolder(pending, DataPathService.Current);
        DataPathText.Text = pending;
        DataPathText.ToolTip = pending;
        DataPathCard.Description = string.Format(
            "{0} · {1}",
            FindString(DataPathService.ModeKeyFor(DataPathService.GetModeFor(pending))),
            FindString(isPending ? "Setting_DataPath_Pending" : "Setting_DataPath_Description"));
    }

    private void PickDataPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = FindString("Setting_DataPath_PickTitle"),
            InitialDirectory = DataPathService.Current,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        ApplyDataPath(dialog.FolderName);
    }

    /// <summary>切换数据目录：先问是否把当前配置复制过去，再写选择记录并提示重启（下次启动生效）。</summary>
    private void ApplyDataPath(string target)
    {
        if (IsSameFolder(target, DataPathService.Current))
        {
            System.Windows.MessageBox.Show(
                FindString("Setting_DataPath_SameFolder"),
                FindString("Setting_DataPath_Title"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            string.Format(FindString("Setting_DataPath_CopyQuestion"), Environment.NewLine),
            FindString("Setting_DataPath_Title"),
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);
        if (answer == System.Windows.MessageBoxResult.Cancel)
        {
            return;
        }

        var copy = answer == System.Windows.MessageBoxResult.Yes;
        if (!DataPathService.SetDataPath(target, copy))
        {
            System.Windows.MessageBox.Show(
                FindString("Setting_DataPath_SetFailed"),
                FindString("Setting_DataPath_Title"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        // 本次运行仍用旧目录：界面显示"待生效"的新目录
        var pending = DataPathService.PendingDataPath;
        DataPathText.Text = pending;
        DataPathText.ToolTip = pending;
        DataPathCard.Description = string.Format(
            "{0} · {1}",
            FindString(DataPathService.ModeKeyFor(DataPathService.GetModeFor(pending))),
            FindString("Setting_DataPath_Pending"));

        if (System.Windows.MessageBox.Show(
                string.Format(FindString("Setting_DataPath_Changed_Message"), pending, Environment.NewLine),
                FindString("Setting_DataPath_Changed_Title"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information) == System.Windows.MessageBoxResult.Yes)
        {
            App.RequestRestart();
        }
    }

    private static bool IsSameFolder(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(
               left.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
               right.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
               StringComparison.OrdinalIgnoreCase);

    private static string FindString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }
}