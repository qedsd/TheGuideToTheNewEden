using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels.Static;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels
{
    /// <summary>
    /// 「攻略 · 入侵」页的 ViewModel。
    /// <para>
    /// 数据只有 15 个站点，来源是 EVE University Wiki 的 <c>Category:Incursions sites</c>；
    /// 维度也简单：**类型**（集结点 / 先锋 / 突击 / 总部 / 母舰）+ 关键词。
    /// 筛选刻意保持精简——帝国、星座、任务类型这些放到正文里看，不占筛选位。
    /// </para>
    /// </summary>
    public sealed class IncursionPageViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<IncursionSite> _items = new();
        private CancellationTokenSource _searchCts;
        private IncursionSite _selected;
        private string _searchText = string.Empty;
        private FilterOption _selectedType;
        private bool _isLoading;
        private bool _isSearchResult;
        private bool _loadFailed;
        private string _error = string.Empty;

        public IncursionPageViewModel()
        {
            RebuildTypeOptions();
            LanguageService.LanguageChanged += OnLanguageChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<IncursionSite> Items => _items;

        public IncursionSite Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value))
                {
                    return;
                }

                _selected = value;
                OnPropertyChanged(nameof(Selected));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(MetaText));
                OnPropertyChanged(nameof(HasMeta));
            }
        }

        public bool HasSelection => _selected is not null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;
                _ = Task.Delay(300, token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        Application.Current?.Dispatcher.Invoke(() => _ = ReloadAsync());
                    }
                }, TaskScheduler.Default);
            }
        }

        public FilterOption SelectedType
        {
            get => _selectedType;
            set
            {
                if (Equals(_selectedType, value))
                {
                    return;
                }

                _selectedType = value;
                OnPropertyChanged(nameof(SelectedType));
                _ = ReloadAsync();
            }
        }

        public ObservableCollection<FilterOption> TypeOptions { get; } = new();

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }

                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsEmpty => !_isLoading && _items.Count == 0;

        public bool LoadFailed
        {
            get => _loadFailed;
            private set
            {
                if (_loadFailed == value)
                {
                    return;
                }

                _loadFailed = value;
                OnPropertyChanged(nameof(LoadFailed));
            }
        }

        public string Error
        {
            get => _error;
            private set
            {
                if (_error == value)
                {
                    return;
                }

                _error = value;
                OnPropertyChanged(nameof(Error));
            }
        }

        /// <summary>
        /// 右侧元信息：站点类型（集结点 / 先锋 / 突击 / 总部 / 母舰）。
        /// <para>
        /// 这里用的是 `RichTextPresenter`（纯文本），**不能带 Markdown 标记**——
        /// 之前写成 `**类型** 集结点`，界面上就把星号原样显示出来了（补充二十七修正）。
        /// </para>
        /// </summary>
        public string MetaText
        {
            get
            {
                if (_selected is null)
                {
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(_selected.TypeCN) ? "入侵站点" : _selected.TypeCN;
            }
        }

        public bool HasMeta => !string.IsNullOrEmpty(MetaText);

        /// <summary>底部状态行（"共 N 个站点" / "搜索到 N 个站点"）。</summary>
        public string StatusText => string.Format(
            FindString(_isSearchResult ? "IncursionPage_SearchSummary" : "IncursionPage_Summary"),
            _items.Count);

        public async Task InitAsync()
        {
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            IsLoading = true;
            LoadFailed = false;
            Error = string.Empty;
            OnPropertyChanged(nameof(StatusText));

            var previousId = _selected?.Id;
            try
            {
                // 下拉用 int.MinValue 表示"不限"，服务层用 AnyType(-1)
                var type = _selectedType is null || _selectedType.Key == int.MinValue
                    ? IncursionService.AnyType
                    : _selectedType.Key;

                var list = await IncursionService.QueryAsync(type, _searchText);

                _items.Clear();
                foreach (var item in list)
                {
                    _items.Add(item);
                }

                _isSearchResult = !string.IsNullOrWhiteSpace(_searchText);
                Selected = _items.FirstOrDefault(x => x.Id == previousId) ?? _items.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                LoadFailed = true;
                Error = ex.Message;
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private void RebuildTypeOptions()
        {
            var keep = _selectedType?.Key ?? int.MinValue;
            TypeOptions.Clear();
            TypeOptions.Add(new FilterOption(int.MinValue, FindString("IncursionPage_AllTypes")));
            foreach (var code in new[] { 0, 1, 2, 3, 4 })
            {
                TypeOptions.Add(new FilterOption(code, FindString(TypeKeyOf(code))));
            }

            _selectedType = TypeOptions.FirstOrDefault(x => x.Key == keep) ?? TypeOptions[0];
            OnPropertyChanged(nameof(SelectedType));
        }

        private static string TypeKeyOf(int type) => type switch
        {
            0 => "IncursionPage_Type_Staging",
            1 => "IncursionPage_Type_Vanguard",
            2 => "IncursionPage_Type_Assault",
            3 => "IncursionPage_Type_Headquarters",
            4 => "IncursionPage_Type_Mothership",
            _ => "IncursionPage_Type_Staging",
        };

        private void OnLanguageChanged(object sender, string language)
        {
            RebuildTypeOptions();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(MetaText));
        }

        private static string FindString(string key)
            => Application.Current?.TryFindResource(key) as string ?? key;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
