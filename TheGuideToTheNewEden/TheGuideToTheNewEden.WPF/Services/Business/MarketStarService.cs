using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 市场物品收藏。与 WinUI 版共用 <c>Configs/StaredMarketInvType.json</c>（一个 int 数组），
/// 增删即时落盘并触发变更事件，供市场树的收藏列表实时更新。
/// </summary>
public sealed class MarketStarService
{
    private static MarketStarService? _current;

    public static MarketStarService Current => _current ??= new MarketStarService();

    private readonly string _staredFile = Path.Combine(SettingsService.DataPath, "Configs", "StaredMarketInvType.json");

    private readonly HashSet<int> _staredTypeIds = [];

    public MarketStarService()
    {
        if (!File.Exists(_staredFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_staredFile);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            var list = JsonConvert.DeserializeObject<List<int>>(json);
            if (list is not null)
            {
                foreach (var id in list)
                {
                    _staredTypeIds.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    public List<int> GetIds() => _staredTypeIds.ToList();

    public bool IsStared(int id) => _staredTypeIds.Contains(id);

    public bool Add(int id)
    {
        if (!_staredTypeIds.Add(id))
        {
            return false;
        }

        Save();
        OnStaredTypeIdsChanged?.Invoke(id, true);
        return true;
    }

    public bool Remove(int id)
    {
        if (!_staredTypeIds.Remove(id))
        {
            return false;
        }

        Save();
        OnStaredTypeIdsChanged?.Invoke(id, false);
        return true;
    }

    public delegate void StaredTypeIdsChangedEventHandler(int id, bool isAdd);

    public event StaredTypeIdsChangedEventHandler? OnStaredTypeIdsChanged;

    private void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(_staredFile);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(_staredFile, JsonConvert.SerializeObject(_staredTypeIds.ToList()));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
