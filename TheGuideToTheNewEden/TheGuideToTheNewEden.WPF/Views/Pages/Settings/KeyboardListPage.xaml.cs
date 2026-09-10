using System.IO;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>游戏按键映射表（只读）。</summary>
public partial class KeyboardListPage : Page
{
    private static readonly string KeyListFile = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Keyboardlist.csv");

    public KeyboardListPage()
    {
        InitializeComponent();

        var items = new List<KeyboardItem>();
        if (File.Exists(KeyListFile))
        {
            foreach (var line in File.ReadAllLines(KeyListFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var item = KeyboardItem.FromCsv(line);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        ResultDataGrid.ItemsSource = items;
    }
}