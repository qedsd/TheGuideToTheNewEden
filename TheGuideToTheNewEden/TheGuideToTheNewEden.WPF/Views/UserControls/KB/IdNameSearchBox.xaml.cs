using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 实体搜索框（对齐 WinUI 的 <c>IdNameSearchBox</c>）：输入名称即在下拉里列出 ZKB 支持的实体，
/// 选中后通过 <see cref="ItemSelected"/> 抛给宿主。
/// 支持按 <see cref="Categories"/> 限定类别；输入纯数字且只限定一个类别时，直接把该数字当 ID。
/// </summary>
public partial class IdNameSearchBox : UserControl
{
    private readonly ObservableCollection<IdName> _results = [];
    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _cts;
    private bool _suppressSelection;

    public IdNameSearchBox()
    {
        InitializeComponent();

        ResultList.ItemsSource = _results;

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer.Stop();
            await SearchAsync();
        };
    }

    /// <summary>限定可选的实体类别（为空表示不限）。</summary>
    public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(
        nameof(Categories), typeof(IdName.CategoryEnum[]), typeof(IdNameSearchBox), new PropertyMetadata(null));

    public IdName.CategoryEnum[]? Categories
    {
        get => (IdName.CategoryEnum[]?)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    /// <summary>选中一个实体。</summary>
    public event Action<IdName>? ItemSelected;

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async Task SearchAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var keyword = SearchTextBox.Text?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            SetResults([]);
            return;
        }

        // 纯数字 + 只限定一个类别 → 直接当 ID 用
        if (int.TryParse(keyword, out var numericId) && numericId > 0 && Categories is { Length: 1 })
        {
            SetResults([new IdName(numericId, numericId.ToString(), Categories[0])]);
            return;
        }

        var found = await ZkbQueryService.SearchEntitiesAsync(keyword, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (Categories is { Length: > 0 })
        {
            found = found.Where(p => Categories.Contains(p.GetCategory())).ToList();
        }

        SetResults(found.Take(30));
    }

    private void SetResults(IEnumerable<IdName> items)
    {
        _suppressSelection = true;
        _results.Clear();
        foreach (var item in items)
        {
            _results.Add(item);
        }

        _suppressSelection = false;
        ResultHost.Visibility = _results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection || ResultList.SelectedItem is not IdName selected)
        {
            return;
        }

        ResultList.SelectedItem = null;
        _results.Clear();
        ResultHost.Visibility = Visibility.Collapsed;
        SearchTextBox.Text = string.Empty;

        ItemSelected?.Invoke(selected);
    }
}
