using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Models.EveSettings;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「同步到…」目标选择对话框（对应 <c>mintnick/eve-settings-manager</c> 的 <c>dialog.syncTitle</c>）：
/// 列出同一配置文件里可被覆盖的同类型文件，勾选后确认。
/// <para>
/// 默认<b>全不选</b>（与参考实现一致：先给用户看清目标再手动挑），"全选"一键勾满。
/// </para>
/// </summary>
public partial class EveSettingsSyncTargetWindow : FluentWindow
{
    private readonly ObservableCollection<EveSettingsSyncTargetItem> _items = [];

    private bool _syncingAllState;

    public EveSettingsSyncTargetWindow(string sourceDisplayName, IReadOnlyList<EveSettingsFileItem> targets)
    {
        InitializeComponent();

        SourceText.Text = string.Format(
            Application.Current?.TryFindResource("EveSettingsSyncPage_SyncSource") as string ?? "{0}",
            sourceDisplayName);

        foreach (var target in targets)
        {
            var item = new EveSettingsSyncTargetItem
            {
                Path = target.Path,
                DisplayName = target.DisplayName,
                SubText = target.FileName,
            };

            item.PropertyChanged += (_, _) => UpdateCount();
            _items.Add(item);
        }

        TargetList.ItemsSource = _items;
        UpdateCount();
    }

    /// <summary>用户勾选的目标文件路径。</summary>
    public IReadOnlyList<string> SelectedPaths
        => _items.Where(p => p.IsChecked).Select(p => p.Path).ToList();

    private void OnSelectAllChecked(object sender, RoutedEventArgs e) => SetAll(true);

    private void OnSelectAllUnchecked(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool value)
    {
        if (_syncingAllState)
        {
            return;
        }

        _syncingAllState = true;
        try
        {
            foreach (var item in _items)
            {
                item.IsChecked = value;
            }
        }
        finally
        {
            _syncingAllState = false;
        }

        UpdateCount();
    }

    private void OnItemChecked(object sender, RoutedEventArgs e)
    {
        if (!_syncingAllState)
        {
            SyncSelectAllState();
        }

        UpdateCount();
    }

    /// <summary>把"全选"复选框的状态与当前勾选情况对齐（有部分选中时显示为不确定态）。</summary>
    private void SyncSelectAllState()
    {
        _syncingAllState = true;
        try
        {
            var checkedCount = _items.Count(p => p.IsChecked);
            SelectAllCheck.IsChecked = checkedCount == 0
                ? false
                : checkedCount == _items.Count
                    ? true
                    : null;
        }
        finally
        {
            _syncingAllState = false;
        }
    }

    private void UpdateCount()
    {
        var text = Application.Current?.TryFindResource("EveSettingsSyncPage_SelectedCount") as string ?? "{0}/{1}";
        CountText.Text = string.Format(text, _items.Count(p => p.IsChecked), _items.Count);
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}

/// <summary>目标选择对话框里的一行。</summary>
public sealed class EveSettingsSyncTargetItem : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Path { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SubText { get; init; } = string.Empty;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
