using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>跳桥列表行（带解析后的星系名）。</summary>
public sealed class JumpBridgeRow
{
    public int System1 { get; init; }
    public int System2 { get; init; }
    public string Name1 { get; init; } = string.Empty;
    public string Name2 { get; init; } = string.Empty;
    public string Text1 => $"{Name1} ({System1})";
    public string Text2 => $"{Name2} ({System2})";
}

/// <summary>
/// 跳桥设置（对齐 WinUI <c>JumpBridgeSetting</c> 控件）：增删跳桥 + 显示开关，
/// 落 <c>Configs/JumpBridgeSetting.json</c>（与 WinUI 共用）。跳桥会作为权重 1 的边参与星门寻路。
/// </summary>
public partial class JumpBridgeSettingView : UserControl
{
    private static readonly Dictionary<int, string> NameCache = [];
    private bool _suppress;

    public JumpBridgeSettingView()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => Reload();
    }

    public ObservableCollection<JumpBridgeRow> Rows { get; } = [];

    /// <summary>跳桥配置发生变化（星图页据此重绘）。</summary>
    public event EventHandler? BridgesChanged;

    public bool ShowInMap
    {
        get => JumpBridgeSettingService.IsShowBridge();
        set
        {
            if (_suppress || ShowInMap == value)
            {
                return;
            }

            JumpBridgeSettingService.SetShowBridge(value);
            BridgesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Reload()
    {
        _suppress = true;
        Rows.Clear();
        foreach (var bridge in JumpBridgeSettingService.GetValue())
        {
            Rows.Add(new JumpBridgeRow
            {
                System1 = bridge.System1,
                System2 = bridge.System2,
                Name1 = ResolveName(bridge.System1),
                Name2 = ResolveName(bridge.System2),
            });
        }

        OnPropertyChanged(nameof(ShowInMap));
        _suppress = false;
        StatusText.Text = string.Format(
            Application.Current?.TryFindResource("MapTool_Bridge_Status") as string ?? "{0}",
            Rows.Count);
    }

    private static string ResolveName(int systemId)
    {
        if (NameCache.TryGetValue(systemId, out var name))
        {
            return name;
        }

        try
        {
            name = Core.Services.DB.MapSolarSystemService.Query(systemId)?.SolarSystemName ?? systemId.ToString();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            name = systemId.ToString();
        }

        NameCache[systemId] = name;
        return name;
    }

    /// <summary>把输入解析成星系 Id：支持 Id 或星系名（精确优先，其次模糊搜索第一条）。</summary>
    private static int ResolveSystemId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        text = text.Trim();
        if (int.TryParse(text, out var id) && id > 0)
        {
            return id;
        }

        try
        {
            var system = Core.Services.DB.MapSolarSystemService.Query(text);
            if (system is not null)
            {
                return system.SolarSystemID;
            }

            var search = Core.Services.DB.MapSolarSystemService.Search(text);
            return search is { Count: > 0 } ? search[0].ID : 0;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var system1 = ResolveSystemId(System1Box.Text);
        var system2 = ResolveSystemId(System2Box.Text);
        if (system1 <= 0 || system2 <= 0)
        {
            StatusText.Text = Application.Current?.TryFindResource("MapTool_Bridge_NotFound") as string ?? string.Empty;
            return;
        }

        if (system1 == system2)
        {
            StatusText.Text = Application.Current?.TryFindResource("MapTool_Bridge_SameSystem") as string ?? string.Empty;
            return;
        }

        if (!JumpBridgeSettingService.Add(system1, system2))
        {
            StatusText.Text = Application.Current?.TryFindResource("MapTool_Bridge_Duplicate") as string ?? string.Empty;
            return;
        }

        System1Box.Clear();
        System2Box.Clear();
        Reload();
        BridgesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not JumpBridgeRow row)
        {
            return;
        }

        JumpBridgeSettingService.Remove(row.System1, row.System2);
        Reload();
        BridgesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
