using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 倒货购物记录：把购物车整体保存为 <c>Configs/ShoppingRecords/yyyy.MM.dd_n.json</c>，可重新载入或加入购物车。
/// 移植自 WinUI 版 <c>Services/ShoppingRecordService</c>（路径改用 WPF 的 SettingsService.DataPath）。
/// </summary>
public sealed class ShoppingRecordService
{
    private static ShoppingRecordService? _current;

    public static ShoppingRecordService Current => _current ??= new ShoppingRecordService();

    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs", "ShoppingRecords");

    /// <summary>记录文件绝对路径列表（按创建顺序，供记录页列表绑定）。</summary>
    public ObservableCollection<string> Files { get; } = [];

    private ShoppingRecordService()
    {
        if (Directory.Exists(Folder))
        {
            foreach (var file in Directory.GetFiles(Folder, "*.json"))
            {
                Files.Add(file);
            }
        }
        else
        {
            Directory.CreateDirectory(Folder);
        }
    }

    /// <summary>保存一批物品为一条新记录，返回文件路径。</summary>
    public string Add(IEnumerable<ScalperShoppingItem> items)
    {
        var date = DateTime.Now.ToString("yyyy.MM.dd");
        var index = 1;
        string path;
        while (true)
        {
            path = Path.Combine(Folder, $"{date}_{index++}.json");
            if (!File.Exists(path))
            {
                break;
            }
        }

        Directory.CreateDirectory(Folder);
        File.WriteAllText(path, JsonConvert.SerializeObject(items));
        Files.Add(path);
        return path;
    }

    public void Remove(string file)
    {
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        Files.Remove(file);
    }

    public List<ScalperShoppingItem>? Load(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<List<ScalperShoppingItem>>(File.ReadAllText(file));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }
}
