using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Services.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>
/// 主权分组编辑（命名分组制，替代旧"分组号"）：左右两列布局——
/// 左列 = 全部联盟列表（搜索过滤；选中分组后逐联盟"加入/移出"），右列 = 分组列表与管理
/// （名称 / 成员数 / 自定义颜色——色相·饱和·亮度弹层取任意色，不再用预设下拉色；
/// 选中分组行内展开详细成员芯片，点芯片移出）。保存写入 MapSettings.json 的 <c>Sov</c> 节；
/// 星图页收到 <see cref="GroupsSaved"/> 后重跑 <c>ApplySovAsync</c> 解析实体并推送画布。
/// </summary>
public partial class SovGroupSettingView : UserControl
{
    private readonly SovGroupRow _ungroupedChoice;   // "未分组"占位（不进配置、不可删）
    private SovGroupRow? _selectedGroup;             // 右列当前选中分组
    private Action<string>? _pickerTarget;           // 颜色弹层确认后写回的目标（分组色/未分组联盟覆盖色）
    private string _filter = string.Empty;

    public SovGroupSettingView()
    {
        InitializeComponent();
        _ungroupedChoice = new SovGroupRow(FindString("MapTool_Sov_Ungrouped"), string.Empty) { IsStub = true };
        DataContext = this;
        Loaded += async (_, _) => await ReloadAsync(false);
    }

    /// <summary>分组列表（编辑后"保存"落盘）。</summary>
    public ObservableCollection<SovGroupRow> Groups { get; } = [];

    /// <summary>全部联盟行。</summary>
    public ObservableCollection<SovAllianceRow> Alliances { get; } = [];

    /// <summary>分组已保存（星图页据此重新解析着色）。</summary>
    public event EventHandler? GroupsSaved;

    // ---------- 装载 ----------

    private async Task ReloadAsync(bool forceRefresh)
    {
        var loadResult = await SovService.LoadAsync(forceRefresh);
        if (!loadResult.Success)
        {
            StatusText.Text = Application.Current?.TryFindResource("MapPage_SovLoadFailed") as string ?? "Load failed";
            return;
        }

        var sov = MapSettingService.Value.Sov ??= new MapSovGroupConfig();

        _selectedGroup = null;
        Groups.Clear();
        _selectedGroup = null;
        foreach (var group in sov.Groups)
        {
            var row = new SovGroupRow(group.Name, group.Color);
            foreach (var allianceId in group.AllianceIds)
            {
                row.AllianceIds.Add(allianceId);
            }

            Groups.Add(row);
        }

        Alliances.Clear();
        foreach (var info in loadResult.Infos)
        {
            var row = new SovAllianceRow(info.AllianceId, info.AllianceName, info.Count)
            {
                Group = Groups.FirstOrDefault(g => g.AllianceIds.Contains(info.AllianceId)) ?? _ungroupedChoice,
                OverrideHex = sov.AllianceColors.TryGetValue(info.AllianceId, out var hex) ? hex : string.Empty,
            };
            Alliances.Add(row);
        }

        foreach (var group in Groups)
        {
            RecountMembers(group);
            group.MembersRebuild(Alliances.Where(a => ReferenceEquals(a.Group, group)));
        }

        RefreshBrushes();
        RefreshLeftList();
        UpdateMemberPanel();
        RefreshJoinButtons();
        UpdateStatus();
    }

    private void UpdateStatus() => StatusText.Text = string.Format(
        Application.Current?.TryFindResource("MapTool_Sov_Status") as string ?? "{0}",
        Alliances.Count);

    // ---------- 保存 / 新建 / 删除 ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var sov = MapSettingService.Value.Sov ??= new MapSovGroupConfig();
        sov.Groups = [.. Groups.Select(g => new MapSovGroup
        {
            Name = g.Name.Trim(),
            Color = g.ColorHex,
            AllianceIds = [.. Alliances.Where(a => ReferenceEquals(a.Group, g)).Select(a => a.AllianceId)],
        })];
        sov.AllianceColors = Alliances
            .Where(a => a.IsUngrouped && !string.IsNullOrEmpty(a.OverrideHex))
            .ToDictionary(a => a.AllianceId, a => a.OverrideHex);
        MapSettingService.Save();

        GroupsSaved?.Invoke(this, EventArgs.Empty);
        StatusText.Text = Application.Current?.TryFindResource("MapTool_Sov_Saved") as string ?? "Saved";
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        // 默认名"分组 N"（避开重名）；默认色轮转 11 档调色板（可在色板上改成任意色）
        var index = Groups.Count;
        var name = string.Format(FindString("MapTool_Sov_GroupDefault"), index + 1);
        while (Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            name = string.Format(FindString("MapTool_Sov_GroupDefault"), index + 1);
        }

        var row = new SovGroupRow(name, Hex(StarMapCanvas.Palette[Groups.Count % StarMapCanvas.Palette.Count]));
        Groups.Add(row);
        GroupList.SelectedItem = row;
    }

    private void DeleteGroup_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SovGroupRow row } && !row.IsStub)
        {
            foreach (var alliance in Alliances.Where(a => ReferenceEquals(a.Group, row)))
            {
                alliance.Group = _ungroupedChoice;   // 成员回落到未分组
            }

            if (ReferenceEquals(_selectedGroup, row))
            {
                _selectedGroup = null;
                GroupList.SelectedItem = null;
                UpdateMemberPanel();
            }

            Groups.Remove(row);
            RefreshBrushes();
            RefreshLeftList();
            RefreshJoinButtons();
            UpdateStatus();
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        SovService.ClearCache();
        await ReloadAsync(true);
        GroupsSaved?.Invoke(this, EventArgs.Empty);
    }

    // ---------- 分组选中 / 成员详细列 ----------

    private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedGroup = GroupList.SelectedItem as SovGroupRow;
        if (_selectedGroup is not null)
        {
            _selectedGroup.MembersRebuild(Alliances.Where(a => ReferenceEquals(a.Group, _selectedGroup)));
        }

        // 列 3（分组详细）：展示选中分组的成员；未选中时显示占位文案
        UpdateMemberPanel();
        RefreshJoinButtons();
    }

    /// <summary>按当前选中分组刷新列 3 的成员列表与占位文案。</summary>
    private void UpdateMemberPanel()
    {
        MemberList.ItemsSource = _selectedGroup?.Members;
        MemberList.Visibility = _selectedGroup is null ? Visibility.Collapsed : Visibility.Visible;
        MemberPlaceholder.Visibility = _selectedGroup is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Join_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup is null || sender is not FrameworkElement { Tag: SovAllianceRow row })
        {
            return;
        }

        var oldGroup = row.Group;
        if (ReferenceEquals(row.Group, _selectedGroup))
        {
            row.Group = _ungroupedChoice;   // 已在选中分组 → 移出
        }
        else
        {
            row.Group = _selectedGroup;     // 加入选中分组
        }

        SyncGroupMembers(oldGroup, row.Group);
        RefreshBrushes();
        RefreshLeftList();
        RefreshJoinButtons();
    }

    private void RemoveMember_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGroup is null || sender is not FrameworkElement { Tag: SovMemberChip chip })
        {
            return;
        }

        var row = Alliances.FirstOrDefault(a => a.AllianceId == chip.AllianceId);
        if (row is null)
        {
            return;
        }

        var oldGroup = row.Group;
        row.Group = _ungroupedChoice;
        SyncGroupMembers(oldGroup, row.Group);
        RefreshBrushes();
        RefreshLeftList();
        RefreshJoinButtons();
    }

    /// <summary>成员变动后：受影响分组的成员数与详细列同步。</summary>
    private void SyncGroupMembers(params SovGroupRow[] affected)
    {
        foreach (var group in affected.Distinct())
        {
            RecountMembers(group);
            if (ReferenceEquals(group, _selectedGroup))
            {
                group.MembersRebuild(Alliances.Where(a => ReferenceEquals(a.Group, group)));
            }
        }
    }

    private void RecountMembers(SovGroupRow group) =>
        group.MemberCount = Alliances.Count(a => ReferenceEquals(a.Group, group));

    // ---------- 联盟行刷新 ----------

    private void RefreshBrushes()
    {
        // 分组先算（未分组联盟的兜底色块可能引用分组色）
        foreach (var group in Groups)
        {
            group.DisplayBrush = SwatchFor(group.ColorHex, -(Groups.IndexOf(group) + 1));
        }

        foreach (var alliance in Alliances)
        {
            alliance.DisplayBrush = alliance.IsUngrouped
                ? SwatchFor(alliance.OverrideHex, alliance.AllianceId)
                : alliance.Group.DisplayBrush;
        }
    }

    /// <summary>hex → 色块笔刷；空/非法按实体键走自动配色。</summary>
    private static Brush SwatchFor(string hex, long autoKey)
    {
        var color = ParseHex(hex) ?? StarMapCanvas.SovGroupColor(autoKey, 0.5);
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    private void RefreshJoinButtons()
    {
        // 左列只显示未分组联盟：有选中分组即可"加入"；已分组联盟只出现在"分组详细"（用移出）
        var hasSelection = _selectedGroup is not null;
        foreach (var alliance in Alliances)
        {
            alliance.ShowJoin = hasSelection && alliance.IsUngrouped;
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = (sender as TextBox)?.Text?.Trim() ?? string.Empty;
        RefreshLeftList();
    }

    /// <summary>
    /// 左列（全部联盟）只显示**未分组**联盟（已分组的去"分组详细"里移出后会回到这里）；
    /// 叠加搜索过滤。分组归属/删除/装载后都要重算。
    /// </summary>
    private void RefreshLeftList()
    {
        foreach (var alliance in Alliances)
        {
            alliance.Visible = alliance.IsUngrouped
                && (_filter.Length == 0 || alliance.AllianceName.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------- 自定义颜色弹层 ----------

    private void Swatch_Click(object sender, MouseButtonEventArgs e)
    {
        string? current;
        switch ((sender as FrameworkElement)?.Tag)
        {
            case SovGroupRow group:
                _pickerTarget = hex => SetGroupColor(group, hex);
                current = group.ColorHex;
                break;
            case SovMemberChip chip when _selectedGroup is not null:
                // 详细列的成员色块 = 所属分组色
                _pickerTarget = hex => SetGroupColor(_selectedGroup, hex);
                current = _selectedGroup.ColorHex;
                break;
            case SovAllianceRow alliance when alliance.IsUngrouped:
                _pickerTarget = hex => SetOverrideColor(alliance, hex);
                current = alliance.OverrideHex;
                break;
            case SovAllianceRow alliance:
                // 分组联盟的色块 = 分组色：点它打开该分组的颜色编辑
                _pickerTarget = hex => SetGroupColor(alliance.Group, hex);
                current = alliance.Group.ColorHex;
                break;
            default:
                return;
        }

        // 延迟到本次鼠标释放后再开弹层：StaysOpen=False 的 Popup 在按下瞬间打开，
        // 同一次点击的 MouseUp 会立刻命中"点外面"判定 → 弹层闪一下就消失（实测）。
        Dispatcher.BeginInvoke(
            () =>
            {
                InitPicker(current);
                ColorPopup.PlacementTarget = this;
                ColorPopup.IsOpen = true;
            },
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void SetGroupColor(SovGroupRow group, string hex)
    {
        group.ColorHex = hex;
    }

    private static void SetOverrideColor(SovAllianceRow alliance, string hex)
    {
        alliance.OverrideHex = hex;
    }

    private void InitPicker(string? hex)
    {
        var sk = ParseHex(hex);
        Picker.SetColor(sk is null ? Color.FromRgb(0x80, 0x80, 0x80) : Color.FromRgb(sk.Value.Red, sk.Value.Green, sk.Value.Blue));
    }

    private void PickerOk_Click(object sender, RoutedEventArgs e)
    {
        var color = Picker.SelectedColor;
        _pickerTarget?.Invoke($"#{color.R:X2}{color.G:X2}{color.B:X2}");
        ColorPopup.IsOpen = false;
        RefreshBrushes();
    }

    // ---------- 工具 ----------

    private static SKColor? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            return SKColor.Parse(hex.Trim());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Hex(SKColor color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>分组编辑行：名称 + 颜色（hex；空 = 自动配色）+ 成员数。</summary>
    public sealed class SovGroupRow : INotifyPropertyChanged
    {
        private string _name;
        private string _colorHex;
        private int _memberCount;
        private Brush _displayBrush = Brushes.Gray;

        public SovGroupRow(string name, string colorHex)
        {
            _name = name;
            _colorHex = colorHex;
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>自定义颜色（#RRGGBB；空串 = 按实体键散列自动配色）。</summary>
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                _colorHex = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<SovMemberChip> Members { get; } = [];

        /// <summary>成员数文本（"成员 N"）。</summary>
        public string MemberCountText => string.Format(FindString("MapTool_Sov_MemberCount"), _memberCount);

        public int MemberCount
        {
            get => _memberCount;
            set
            {
                _memberCount = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MemberCountText));
            }
        }

        /// <summary>占位"未分组"项不可删除、不进配置。</summary>
        public bool IsStub { get; init; }

        /// <summary>成员联盟 ID（重载时回填，供"联盟 → 分组"反查）。</summary>
        public List<long> AllianceIds { get; } = [];

        public Brush DisplayBrush
        {
            get => _displayBrush;
            set
            {
                _displayBrush = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>由视图在成员/配色变化后调用：重算成员列表（列 3 行：色块｜名称｜领地数）。</summary>
        public void MembersRebuild(IEnumerable<SovAllianceRow> members)
        {
            Members.Clear();
            foreach (var member in members)
            {
                Members.Add(new SovMemberChip(member.AllianceId, member.AllianceName, member.Count));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>分组详细列的一个成员行（点击移出该分组）。</summary>
    public sealed record SovMemberChip(long AllianceId, string Name, int Count);

    /// <summary>联盟行：归属分组 + 未分组颜色覆盖 + 与选中分组的加入/移出按钮状态。</summary>
    public sealed class SovAllianceRow : INotifyPropertyChanged
    {
        private SovGroupRow _group;
        private string _overrideHex = string.Empty;
        private Brush _displayBrush = Brushes.Gray;
        private bool _showJoin;
        private bool _visible = true;

        public SovAllianceRow(long allianceId, string allianceName, int count)
        {
            AllianceId = allianceId;
            AllianceName = allianceName;
            Count = count;
        }

        public long AllianceId { get; }

        public string AllianceName { get; }

        public int Count { get; }

        /// <summary>所属分组（占位项 = 未分组）。</summary>
        public SovGroupRow Group
        {
            get => _group;
            set
            {
                _group = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>未分组联盟的颜色覆盖（#RRGGBB；空 = 自动配色）；分组联盟跟随分组色。</summary>
        public string OverrideHex
        {
            get => _overrideHex;
            set
            {
                _overrideHex = value;
                RaisePropertyChanged();
            }
        }

        public bool IsUngrouped => Group.IsStub;

        public Brush DisplayBrush
        {
            get => _displayBrush;
            set
            {
                _displayBrush = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowJoin
        {
            get => _showJoin;
            set
            {
                _showJoin = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>搜索过滤命中（false 时整行隐藏）。</summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
