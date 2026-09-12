using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Business;

/// <summary>
/// 购物记录页：列出已保存的购物记录，载入查看明细、删除记录、把明细加回购物车。
/// </summary>
public sealed class ScalperShoppingRecordViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> Files => ShoppingRecordService.Current.Files;

    private string? _selectedFile;

    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (Set(ref _selectedFile, value))
            {
                LoadSelected();
            }
        }
    }

    private List<ScalperShoppingItem> _items = [];

    public List<ScalperShoppingItem> Items
    {
        get => _items;
        private set => Set(ref _items, value);
    }

    public bool HasItems => Items.Count > 0;

    private void LoadSelected()
    {
        Items = string.IsNullOrEmpty(SelectedFile)
            ? []
            : ShoppingRecordService.Current.Load(SelectedFile) ?? [];
        OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>删除记录文件；返回实际删除的数量。</summary>
    public int RemoveFiles(IEnumerable<string> files)
    {
        var list = files.ToList();
        foreach (var file in list)
        {
            ShoppingRecordService.Current.Remove(file);
        }

        if (SelectedFile is not null && list.Contains(SelectedFile))
        {
            SelectedFile = null;
        }

        return list.Count;
    }

    /// <summary>把选中记录明细加回购物车。</summary>
    public int AddToCart(IEnumerable<ScalperShoppingItem> items)
    {
        var list = items.ToList();
        foreach (var item in list)
        {
            BusinessService.Current.ShoppingCart.Add(item.DepthClone<ScalperShoppingItem>());
        }

        return list.Count;
    }

    /// <summary>记录文件名（不含目录与扩展名），供列表展示。</summary>
    public static string DisplayName(string path) => Path.GetFileNameWithoutExtension(path);

    // ---------- INotifyPropertyChanged ----------

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
