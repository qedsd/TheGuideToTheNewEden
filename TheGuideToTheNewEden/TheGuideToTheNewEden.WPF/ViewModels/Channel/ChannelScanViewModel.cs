using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.CharacterScan;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道统计页：把游戏频道玩家列表 Ctrl+C 复制的角色名粘贴进来，
/// 经 本地IDName库/ESI 名称解析 → ESI Affiliation（军团/联盟归属）→ 军团/联盟人数统计
/// → ZKB 战绩（击杀/损失/单挑率/威胁值/常用船与常出没地）。
/// 流程与 WinUI 版 <c>ChannelScanViewModel</c> 一致；等待/错误提示走 <see cref="PageNotifyService"/>。
/// </summary>
public sealed class ChannelScanViewModel : INotifyPropertyChanged
{
    private string _namesStr = string.Empty;

    public string NamesStr
    {
        get => _namesStr;
        set => Set(ref _namesStr, value);
    }

    private bool _isSetting;

    /// <summary>设置面板显示中。</summary>
    public bool IsSetting
    {
        get => _isSetting;
        set => Set(ref _isSetting, value);
    }

    private bool _isAddingIgnore;

    public bool IsAddingIgnore
    {
        get => _isAddingIgnore;
        set => Set(ref _isAddingIgnore, value);
    }

    private string _addingIgnoreId = string.Empty;

    public string AddingIgnoreId
    {
        get => _addingIgnoreId;
        set => Set(ref _addingIgnoreId, value);
    }

    private string _addingIgnoreName = string.Empty;

    public string AddingIgnoreName
    {
        get => _addingIgnoreName;
        set => Set(ref _addingIgnoreName, value);
    }

    private int _addingIgnoreCategory;

    /// <summary>忽略名单类型下拉索引（0 角色 / 1 军团 / 2 联盟）。</summary>
    public int AddingIgnoreCategory
    {
        get => _addingIgnoreCategory;
        set => Set(ref _addingIgnoreCategory, value);
    }

    private int _resultCount;

    public int ResultCount
    {
        get => _resultCount;
        private set => Set(ref _resultCount, value);
    }

    public ChannelScanConfig Config { get; }

    private ObservableCollection<CharacterScanInfo> _scanInfos = [];

    public ObservableCollection<CharacterScanInfo> ScanInfos
    {
        get => _scanInfos;
        private set => Set(ref _scanInfos, value);
    }

    private List<ScanStatisticsItem> _statisticsCorporation = [];

    /// <summary>军团人数统计（人数降序，含军团徽标）。</summary>
    public List<ScanStatisticsItem> StatisticsCorporation
    {
        get => _statisticsCorporation;
        private set => Set(ref _statisticsCorporation, value);
    }

    private List<ScanStatisticsItem> _statisticsAlliance = [];

    /// <summary>联盟人数统计（人数降序，含联盟徽标）。</summary>
    public List<ScanStatisticsItem> StatisticsAlliance
    {
        get => _statisticsAlliance;
        private set => Set(ref _statisticsAlliance, value);
    }

    public ChannelScanViewModel()
    {
        Config = ChannelScanSettingService.GetChannelScanConfig();
        Config.PropertyChanged += (_, _) => SaveConfig();
    }

    /// <summary>执行分析（对齐 WinUI 版 StartCommand 全流程）。</summary>
    public async Task StartAsync()
    {
        PageNotifyService.ShowWaiting(FindString("ChannelScanPage_Start"));
        try
        {
            ResultCount = 0;
            var names = GetNames(NamesStr);
            var namesAfterFiltered = GetFilteredNames(names);
            if (namesAfterFiltered is not { Count: > 0 })
            {
                PageNotifyService.Error(FindString("ChannelScanPage_NoValidName"));
                return;
            }

            // 忽略名单 ID 集
            var ignoredCharacterIds = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Character).Select(p => (long)p.Id).ToHashSet();
            var ignoredCorpIds = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Corporation).Select(p => (long)p.Id).ToHashSet();
            var ignoredAllianceIds = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Alliance).Select(p => (long)p.Id).ToHashSet();

            var allNamesDatas = await Core.Services.IDNameService.GetByNames(namesAfterFiltered);
            if (allNamesDatas is not { Count: > 0 })
            {
                PageNotifyService.Error(FindString("ChannelScanPage_NoValidName"));
                return;
            }

            var characterNames = allNamesDatas.Where(p => p.GetCategory() == IdName.CategoryEnum.Character).ToArray();
            if (characterNames.Length == 0)
            {
                PageNotifyService.Error(FindString("ChannelScanPage_NoValidName"));
                return;
            }

            var scanInfos = new ObservableCollection<CharacterScanInfo>();
            int start = 0;
            int length = Math.Min(1000, characterNames.Length);
            while (true)
            {
                // ESI 角色归属（1000/批）
                var affiliationResult = await ESIService.Current.EsiClient.Character.AffiliationAsync(
                    characterNames.Skip(start).Take(length).Select(p => (long)p.Id).ToList());
                if (affiliationResult?.Model is not { Count: > 0 })
                {
                    break;
                }

                var affiliations = affiliationResult.Model;
                if (!Config.ShowIgnoredInResultDetail && !Config.ShowIgnoredInResultStatistics)
                {
                    // 统计与详细都不展示忽略对象时，提前过滤（可少查一轮名称）
                    affiliations = affiliations.Where(p =>
                        !ignoredCharacterIds.Contains(p.CharacterId)
                        && !ignoredCorpIds.Contains(p.CorporationId)
                        && !ignoredAllianceIds.Contains(p.AllianceId ?? 0)).ToList();
                }

                var datas = (await Task.Run(() => Core.Helpers.ThreadHelper.RunAsync(affiliations, affiliation =>
                    CharacterScanInfo.Create(affiliation.CharacterId, affiliation.CorporationId, affiliation.AllianceId ?? 0))))
                    .Where(p => p is not null)
                    .Select(p => p!)
                    .ToList();

                // 军团 / 联盟人数统计（剔除忽略成员的实际数量）
                StatisticsCorporation = BuildStatistics(datas, ignoredCharacterIds, ignoredCorpIds, p => p.Corporation.Id, p => p.Corporation, IdName.CategoryEnum.Corporation, isAlliance: false);
                StatisticsAlliance = BuildStatistics(datas, ignoredCharacterIds, ignoredAllianceIds, p => p.Alliance.Id, p => p.Alliance, IdName.CategoryEnum.Alliance, isAlliance: true);

                // 按输入名单顺序重排
                var datasDic = datas.DistinctBy(p => p.Name).ToDictionary(p => p.Name!);
                foreach (var name in namesAfterFiltered)
                {
                    if (datasDic.TryGetValue(name, out var info))
                    {
                        scanInfos.Add(info);
                    }
                }

                var found = start + length;
                var remain = characterNames.Length - found;
                if (remain > 0)
                {
                    // 步进必须用本批长度：found = start + length 是"已处理到的位置"，
                    // 用它做 += 在多批（>1000 名）时会跳批漏人
                    start += length;
                    length = Math.Min(1000, remain);
                }
                else
                {
                    break;
                }
            }

            if (scanInfos.Count == 0)
            {
                PageNotifyService.Error(FindString("ChannelScanPage_NoValidName"));
                return;
            }

            // ZKB 战绩（限制数量，多线程拉取）
            if (Config.GetZKB)
            {
                var needGetZkb = BuildZkbList(scanInfos, ignoredCharacterIds, ignoredCorpIds, ignoredAllianceIds);
                await Task.Run(() => Core.Helpers.ThreadHelper.Run(needGetZkb, data => data.GetZKBInfo()));
            }

            ScanInfos = scanInfos;
            ResultCount = ScanInfos.Count;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    private List<ScanStatisticsItem> BuildStatistics(
        List<CharacterScanInfo> datas,
        HashSet<long> ignoredCharacterIds,
        HashSet<long> ignoredGroupIds,
        Func<CharacterScanInfo, long> groupKey,
        Func<CharacterScanInfo, IdName> groupName,
        IdName.CategoryEnum category,
        bool isAlliance)
    {
        var statistics = new List<ScanStatisticsItem>();
        foreach (var group in datas.GroupBy(groupKey).OrderByDescending(p => p.Count()))
        {
            if (!Config.ShowIgnoredInResultStatistics && ignoredGroupIds.Contains(group.Key))
            {
                continue;
            }

            var first = group.First();
            var idName = new IdName(groupName(first).Id, groupName(first).Name, category);
            var count = Config.ShowIgnoredInResultStatistics
                ? group.Count()
                : group.Count(p => !ignoredCharacterIds.Contains(p.Id));
            statistics.Add(new ScanStatisticsItem
            {
                Entity = idName,
                Count = count,
                IsAlliance = isAlliance,
            });
        }

        // 逐项异步加载军团/联盟徽标（失败不影响列表）
        foreach (var item in statistics)
        {
            _ = item.LoadLogoAsync();
        }

        return statistics;
    }

    private List<CharacterScanInfo> BuildZkbList(
        ObservableCollection<CharacterScanInfo> scanInfos,
        HashSet<long> ignoredCharacterIds,
        HashSet<long> ignoredCorpIds,
        HashSet<long> ignoredAllianceIds)
    {
        var result = new List<CharacterScanInfo>();
        var max = Math.Max(1, (int)Config.MaxZKB);
        if (!Config.ShowIgnoredInResultDetail)
        {
            foreach (var data in scanInfos)
            {
                if (ignoredCharacterIds.Contains(data.Character.Id)
                    || ignoredCorpIds.Contains(data.Corporation.Id)
                    || ignoredAllianceIds.Contains(data.Alliance.Id))
                {
                    continue;
                }

                result.Add(data);
                if (result.Count == max)
                {
                    break;
                }
            }
        }
        else
        {
            result = scanInfos.Take(max).ToList();
        }

        return result;
    }

    private static List<string>? GetNames(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return null;
        }

        // 按任意换行符切分：粘贴来源可能是 CRLF、LF（游戏中复制的名单就是 LF）或 CR，
        // WinUI 原实现只按 '\r' 切分，遇到 LF-only 会把整段当成一个名字而全部识别失败。
        var names = new List<string>();
        foreach (var line in str.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private List<string>? GetFilteredNames(List<string>? names)
    {
        if (names is not { Count: > 0 })
        {
            return names;
        }

        var ignoredCharacters = Config.Ignoreds.Where(p => p.GetCategory() == IdName.CategoryEnum.Character).Select(p => p.Name).ToHashSet();
        if (ignoredCharacters.Count == 0)
        {
            return names;
        }

        return names.Where(p => !ignoredCharacters.Contains(p)).ToList();
    }

    // ---------- 忽略名单 ----------

    public void ShowAddIgnore()
    {
        AddingIgnoreId = string.Empty;
        AddingIgnoreName = string.Empty;
        IsAddingIgnore = true;
    }

    public void CancelAddIgnore() => IsAddingIgnore = false;

    public void ConfirmAddIgnore()
    {
        var id = -1;
        if (!string.IsNullOrEmpty(AddingIgnoreId) && !int.TryParse(AddingIgnoreId, out id))
        {
            PageNotifyService.Error(FindString("ChannelScanPage_Setting_AddIgnoredIdInvalid"));
            return;
        }

        if (string.IsNullOrEmpty(AddingIgnoreName))
        {
            PageNotifyService.Error(FindString("ChannelScanPage_Setting_AddIgnoredNameInvalid"));
            return;
        }

        if (Config.Ignoreds.FirstOrDefault(p => p.Id != -1 && p.Id == id || p.Name == AddingIgnoreName) is not null)
        {
            PageNotifyService.Error(FindString("ChannelScanPage_Setting_AddIgnoredSame"));
            return;
        }

        var category = AddingIgnoreCategory switch
        {
            1 => IdName.CategoryEnum.Corporation,
            2 => IdName.CategoryEnum.Alliance,
            _ => IdName.CategoryEnum.Character,
        };
        Config.Ignoreds.Add(new IdName(id, AddingIgnoreName, category));
        SaveConfig();
        PageNotifyService.Success(FindString("ChannelScanPage_Setting_AddIgnoredSuccessful"));
        IsAddingIgnore = false;
    }

    public void DeleteIgnore(IdName item)
    {
        Config.Ignoreds.Remove(item);
        SaveConfig();
    }

    /// <summary>表格右键"忽略该角色/军团/联盟"。</summary>
    public void AddIgnore(IdName idName)
    {
        if (idName.Id <= 0)
        {
            return;
        }

        if (Config.Ignoreds.FirstOrDefault(p => p.Id != -1 && p.Id == idName.Id || p.Name == idName.Name) is not null)
        {
            PageNotifyService.Error(FindString("ChannelScanPage_Setting_AddIgnoredSame"));
            return;
        }

        Config.Ignoreds.Add(idName);
        SaveConfig();
        PageNotifyService.Success(FindString("ChannelScanPage_Setting_AddIgnoredSuccessful"));
    }

    /// <summary>重新获取某角色的 ZKB 战绩（原位替换）。</summary>
    public async Task ReloadZKBInfoAsync(CharacterScanInfo info)
    {
        var index = ScanInfos.IndexOf(info);
        if (index < 0)
        {
            return;
        }

        ScanInfos.RemoveAt(index);
        PageNotifyService.ShowWaiting(FindString("ChannelScanPage_ReloadResultZKB"));
        try
        {
            var clone = new CharacterScanInfo
            {
                Character = info.Character,
                Corporation = info.Corporation,
                Alliance = info.Alliance,
                Faction = info.Faction,
            };
            await Task.Run(() => clone.GetZKBInfo());
            ScanInfos.Insert(index, clone);
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }
    }

    private void SaveConfig() => ChannelScanSettingService.Save(Config);

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

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
