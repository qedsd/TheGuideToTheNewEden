using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>ESI 权限范围设置：勾选结果即时保存。</summary>
public partial class ESIScopeSettingPage : Page
{
    private readonly ObservableCollection<ScopeItem> _items = [];
    private bool _suppressAutoSave;

    public ESIScopeSettingPage()
    {
        InitializeComponent();

        var service = ESIScopeService.Current;
        var selected = service.GetSelectedScopes().ToHashSet(StringComparer.Ordinal);

        foreach (var scope in service.GetAllScopes())
        {
            var item = new ScopeItem(scope, selected.Contains(scope));
            item.PropertyChanged += OnScopeChanged;
            _items.Add(item);
        }

        ScopeList.ItemsSource = _items;

        SelectAllCheckBox.IsChecked = _items.Count > 0 && _items.All(i => i.IsSelected);
        SelectAllCheckBox.Click += OnSelectAllClick;
    }

    private void OnScopeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressAutoSave || sender is not ScopeItem item || e.PropertyName != nameof(ScopeItem.IsSelected))
        {
            return;
        }

        var service = ESIScopeService.Current;
        if (item.IsSelected)
        {
            service.SelectScope(item.Scope);
        }
        else
        {
            service.CancelSelectScope(item.Scope);
        }

        _suppressAutoSave = true;
        SelectAllCheckBox.IsChecked = _items.All(i => i.IsSelected);
        _suppressAutoSave = false;
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoSave)
        {
            return;
        }

        var selectAll = SelectAllCheckBox.IsChecked == true;
        var service = ESIScopeService.Current;

        _suppressAutoSave = true;
        foreach (var item in _items)
        {
            item.IsSelected = selectAll;
        }

        _suppressAutoSave = false;

        if (selectAll)
        {
            service.SelectAllScope();
        }
        else
        {
            service.CancelSelectAllScope();
        }
    }

    /// <summary>权限项视图模型。</summary>
    private sealed class ScopeItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public ScopeItem(string scope, bool isSelected)
        {
            Scope = scope;
            _isSelected = isSelected;
        }

        public string Scope { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}