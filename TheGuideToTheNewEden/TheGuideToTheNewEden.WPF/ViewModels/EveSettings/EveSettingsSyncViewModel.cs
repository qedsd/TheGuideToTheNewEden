using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Models.EveSettings;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.EveSettings;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.EveSettings;

/// <summary>
/// 「配置同步」页面（EVE 客户端设置文件：角色/账户 <c>.dat</c> 之间同步 + 备份/还原 + 配置文件管理）。
/// <para>
/// 分层遵循本项目惯例：VM 负责数据与操作 + 等待态/通知，页面代码后置负责
/// 确认对话框、"同步到…"目标选择窗、文件夹选择器等纯界面交互。
/// </para>
/// <para>
/// <b>写操作前的游戏运行检测</b>由页面统一把关（<c>ConfirmWrite</c>），VM 只提供
/// <see cref="IsGameRunning"/> / <see cref="RunningGameText"/>；原因是"检测到了是警告还是拦截"
/// 是界面决策（可配置），不该埋进数据层。
/// </para>
/// </summary>
public sealed class EveSettingsSyncViewModel : INotifyPropertyChanged
{
    /// <summary>每次加载自增：旧的后台角色名解析结果不得覆盖新选择。</summary>
    private int _loadVersion;

    private string _rootPath = string.Empty;
    private bool _hasRoot;
    private bool _isBusy;
    private string _statusText = string.Empty;
    private bool _isGameRunning;
    private string _runningGameText = string.Empty;
    private EveSettingsServer? _selectedServer;
    private EveSettingsProfile? _selectedProfile;
    private bool _autoBackupBeforeSync = true;
    private bool _blockWhenGameRunning = true;

    // ---------- 集合 ----------

    public ObservableCollection<EveSettingsServer> Servers { get; } = [];

    public ObservableCollection<EveSettingsProfile> Profiles { get; } = [];

    public ObservableCollection<EveSettingsFileItem> CharacterFiles { get; } = [];

    public ObservableCollection<EveSettingsFileItem> AccountFiles { get; } = [];

    public ObservableCollection<EveSettingsFileItem> UnrecognizedFiles { get; } = [];

    public ObservableCollection<EveSettingsBackupItem> Backups { get; } = [];

    // ---------- 状态 ----------

    /// <summary>当前使用的 EVE 设置根目录。</summary>
    public string RootPath
    {
        get => _rootPath;
        private set => Set(ref _rootPath, value);
    }

    public bool HasRoot
    {
        get => _hasRoot;
        private set => Set(ref _hasRoot, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanOperate));
                OnPropertyChanged(nameof(CanManageProfiles));
                OnPropertyChanged(nameof(CanManageFiles));
            }
        }
    }

    /// <summary>界面按钮的可用性（忙的时候整体禁用，避免并发写同一批文件）。</summary>
    public bool CanOperate => !_isBusy;

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set => Set(ref _isGameRunning, value);
    }

    /// <summary>游戏运行提示文案（含正在运行的角色名，能取到时）。</summary>
    public string RunningGameText
    {
        get => _runningGameText;
        private set => Set(ref _runningGameText, value);
    }

    public EveSettingsServer? SelectedServer
    {
        get => _selectedServer;
        private set
        {
            if (Set(ref _selectedServer, value))
            {
                OnPropertyChanged(nameof(CanManageProfiles));
            }
        }
    }

    public EveSettingsProfile? SelectedProfile
    {
        get => _selectedProfile;
        private set
        {
            if (Set(ref _selectedProfile, value))
            {
                OnPropertyChanged(nameof(CanManageFiles));
            }
        }
    }

    /// <summary>同步 / 还原前先自动整配置文件备份。</summary>
    public bool AutoBackupBeforeSync
    {
        get => _autoBackupBeforeSync;
        set
        {
            if (Set(ref _autoBackupBeforeSync, value))
            {
                EveSettingsSettingService.Value.AutoBackupBeforeSync = value;
                EveSettingsSettingService.Save();
            }
        }
    }

    /// <summary>检测到 EVE 客户端在运行时先拦截（需用户二次确认才继续写）。</summary>
    public bool BlockWhenGameRunning
    {
        get => _blockWhenGameRunning;
        set
        {
            if (Set(ref _blockWhenGameRunning, value))
            {
                EveSettingsSettingService.Value.BlockWhenGameRunning = value;
                EveSettingsSettingService.Save();
            }
        }
    }

    // ---------- 空态 / 可用性 ----------

    public bool HasServers => Servers.Count > 0;

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasCharacterFiles => CharacterFiles.Count > 0;

    public bool HasAccountFiles => AccountFiles.Count > 0;

    public bool HasUnrecognizedFiles => UnrecognizedFiles.Count > 0;

    public bool HasBackups => Backups.Count > 0;

    public int UnrecognizedCount => UnrecognizedFiles.Count;

    public bool CanManageProfiles => SelectedServer is not null && !_isBusy;

    public bool CanManageFiles => SelectedProfile is not null && !_isBusy;

    public bool CanDeleteProfile => Profiles.Count > 1;

    public string BackupRoot => EveSettingsBackupService.GetBackupRoot();

    public string ServerDirName => SelectedServer?.DirName ?? "unknown";

    public string ProfileName => SelectedProfile?.Name ?? "unknown";

    // ---------- 加载 ----------

    public async Task<bool> InitializeAsync()
    {
        var setting = EveSettingsSettingService.Value;
        _autoBackupBeforeSync = setting.AutoBackupBeforeSync;
        _blockWhenGameRunning = setting.BlockWhenGameRunning;
        OnPropertyChanged(nameof(AutoBackupBeforeSync));
        OnPropertyChanged(nameof(BlockWhenGameRunning));

        RootPath = EveSettingsFolderService.NormalizeRoot(setting.RootFolder);

        RefreshGameState();
        await ReloadAsync();
        return HasRoot;
    }

    /// <summary>换一个设置根目录（用户手选，已由 <see cref="EveSettingsFolderService.ResolveRootFromSelection"/> 上溯）。</summary>
    public async Task SetRootAsync(string path)
    {
        RootPath = EveSettingsFolderService.NormalizeRoot(path);
        EveSettingsSettingService.Value.RootFolder = RootPath;
        EveSettingsSettingService.Save();
        await ReloadAsync();
    }

    /// <summary>重新扫描：服务器 → 配置文件 → 文件 → 备份。</summary>
    public async Task ReloadAsync()
    {
        _loadVersion++;
        IsBusy = true;
        try
        {
            Servers.Clear();
            Profiles.Clear();
            SelectedServer = null;
            SelectedProfile = null;
            ClearFiles();

            HasRoot = EveSettingsFolderService.Exists(RootPath);
            if (!HasRoot)
            {
                await LoadBackupsAsync();
                return;
            }

            foreach (var server in EveSettingsFolderService.GetServers(RootPath))
            {
                Servers.Add(server);
            }

            if (Servers.Count == 0)
            {
                await LoadBackupsAsync();
                return;
            }

            // 默认选中：上次用过的 → 本应用当前游戏服务器对应的 → 第一个
            var setting = EveSettingsSettingService.Value;
            var wanted = Servers.FirstOrDefault(p => string.Equals(p.DirName, setting.LastServerDir, StringComparison.OrdinalIgnoreCase))
                         ?? Servers.FirstOrDefault(p => p.Kind == CurrentGameServerKind())
                         ?? Servers[0];

            await SelectServerAsync(wanted);
            await LoadBackupsAsync();
        }
        finally
        {
            IsBusy = false;
            UpdateStates();
        }
    }

    public async Task SelectServerAsync(EveSettingsServer? server)
    {
        SelectedServer = server;
        Profiles.Clear();
        SelectedProfile = null;
        ClearFiles();

        if (server is null)
        {
            UpdateStates();
            return;
        }

        var setting = EveSettingsSettingService.Value;
        setting.LastServerDir = server.DirName;
        EveSettingsSettingService.Save();

        foreach (var profile in EveSettingsFolderService.GetProfiles(server.Path))
        {
            Profiles.Add(profile);
        }

        var last = setting.LastProfileByServer.TryGetValue(server.DirName, out var remembered) ? remembered : null;
        var wanted = Profiles.FirstOrDefault(p => string.Equals(p.Name, last, StringComparison.OrdinalIgnoreCase))
                     ?? Profiles.FirstOrDefault(p => string.Equals(p.Name, "Default", StringComparison.OrdinalIgnoreCase))
                     ?? Profiles.FirstOrDefault();

        await SelectProfileAsync(wanted);
    }

    public async Task SelectProfileAsync(EveSettingsProfile? profile)
    {
        SelectedProfile = profile;
        ClearFiles();

        if (profile is null || SelectedServer is null)
        {
            UpdateStates();
            return;
        }

        var setting = EveSettingsSettingService.Value;
        setting.LastProfileByServer[SelectedServer.DirName] = profile.Name;
        EveSettingsSettingService.Save();

        var version = ++_loadVersion;
        foreach (var file in EveSettingsFolderService.GetFiles(profile.Path))
        {
            switch (file.Kind)
            {
                case EveSettingsFileKind.Character:
                    CharacterFiles.Add(file);
                    break;
                case EveSettingsFileKind.Account:
                    AccountFiles.Add(file);
                    break;
                default:
                    UnrecognizedFiles.Add(file);
                    break;
            }
        }

        UpdateStates();

        if (CharacterFiles.Count == 0)
        {
            return;
        }

        StatusText = FindString("EveSettingsSyncPage_ResolvingNames");
        try
        {
            await ResolveNamesAsync(version);
        }
        finally
        {
            if (version == _loadVersion)
            {
                StatusText = string.Empty;
            }
        }
    }

    /// <summary>解析角色名并就地回填（失败只记日志，列表退回显示 ID）。</summary>
    private async Task ResolveNamesAsync(int version)
    {
        if (SelectedServer is null)
        {
            return;
        }

        var ids = CharacterFiles
            .Where(p => string.IsNullOrEmpty(p.ResolvedName) && p.Id > 0)
            .Select(p => p.Id)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        var names = await EveSettingsNameResolver.ResolveAsync(ids, SelectedServer.Kind);
        if (version != _loadVersion)
        {
            return; // 期间换过服务器/配置文件，旧结果作废
        }

        foreach (var file in CharacterFiles)
        {
            if (names.TryGetValue(file.Id, out var name))
            {
                file.ResolvedName = name;
            }
        }
    }

    public async Task LoadBackupsAsync()
    {
        Backups.Clear();
        foreach (var backup in await Task.Run(EveSettingsBackupService.GetBackups))
        {
            Backups.Add(backup);
        }

        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(BackupRoot));
    }

    // ---------- 同步 ----------

    /// <summary>可同步的目标：同一配置文件目录内、同一类型、且不是源文件自己。</summary>
    public IReadOnlyList<EveSettingsFileItem> GetSyncTargets(EveSettingsFileItem source)
    {
        var pool = source.Kind switch
        {
            EveSettingsFileKind.Character => CharacterFiles,
            EveSettingsFileKind.Account => AccountFiles,
            _ => UnrecognizedFiles,
        };

        return pool
            .Where(p => p.Kind == source.Kind && !string.Equals(p.Path, source.Path, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 执行同步（源 → 多个目标，整体覆盖）。
    /// 开启「同步前自动备份」时先整配置文件备份一次；备份失败即中止（不冒"改坏了又没退路"的风险）。
    /// </summary>
    public async Task<EveSettingsSyncResult> SyncAsync(EveSettingsFileItem source, IReadOnlyList<string> targetPaths)
    {
        var result = new EveSettingsSyncResult();
        if (targetPaths.Count == 0)
        {
            result.Error = FindString("EveSettingsSyncPage_SyncNoTargets");
            return result;
        }

        string? backupName = null;
        if (AutoBackupBeforeSync)
        {
            var profilePath = Path.GetDirectoryName(source.Path);
            if (!string.IsNullOrEmpty(profilePath))
            {
                var name = EveSettingsBackupService.EnsureUniqueName(
                    EveSettingsBackupService.BuildFolderBackupName(ServerDirName, ProfileName));
                var display = BuildBackupDisplayName();

                var (backup, backupError) = await Task.Run(() =>
                {
                    var item = EveSettingsBackupService.CreateFolderBackup(profilePath, name, display, out var error);
                    return (item, error);
                });

                if (backup is null)
                {
                    var message = string.Format(FindString("EveSettingsSyncPage_BackupFailed"), backupError ?? string.Empty);
                    PageNotifyService.Error(message);
                    result.Error = message;
                    return result;
                }

                backupName = backup.Name;
                await LoadBackupsAsync();
            }
        }

        using var cancellation = new CancellationTokenSource();
        var progressFormat = FindString("EveSettingsSyncPage_Syncing");
        PageNotifyService.ShowWaiting(FindString("EveSettingsSyncPage_SyncingNow"), cancellation.Cancel);

        try
        {
            var targets = targetPaths.ToList();
            var progress = new Progress<(int Done, int Total)>(p =>
                PageNotifyService.UpdateWaiting(string.Format(progressFormat, p.Done, p.Total)));

            result = await Task.Run(
                () => EveSettingsSyncService.Sync(source.Path, targets, progress, cancellation.Token),
                cancellation.Token);
            result.AutoBackupName = backupName;
        }
        catch (OperationCanceledException)
        {
            result = new EveSettingsSyncResult { Cancelled = true, AutoBackupName = backupName };
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }

        NotifySyncResult(result);

        // 覆盖后文件大小/时间都变了，刷新当前列表让界面与磁盘一致。
        if (SelectedProfile is not null)
        {
            await SelectProfileAsync(SelectedProfile);
        }

        return result;
    }

    private void NotifySyncResult(EveSettingsSyncResult result)
    {
        if (result.Cancelled)
        {
            PageNotifyService.Info(FindString("EveSettingsSyncPage_Cancelled"));
            return;
        }

        if (result.Error is not null)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_SyncFailed"), result.Error));
            return;
        }

        var text = string.Format(FindString("EveSettingsSyncPage_SyncDone"), result.Succeeded, result.Failed);
        if (result.AutoBackupName is not null)
        {
            text += " " + string.Format(FindString("EveSettingsSyncPage_BackupSuffix"), result.AutoBackupName);
        }

        if (result.Failed > 0)
        {
            PageNotifyService.Warning(text);
        }
        else
        {
            PageNotifyService.Success(text);
        }
    }

    // ---------- 备份 / 还原 ----------

    /// <summary>备份整个当前配置文件。</summary>
    public async Task<EveSettingsBackupItem?> BackupProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return null;
        }

        var name = EveSettingsBackupService.EnsureUniqueName(
            EveSettingsBackupService.BuildFolderBackupName(ServerDirName, SelectedProfile.Name));
        var display = BuildBackupDisplayName();

        PageNotifyService.ShowWaiting(FindString("EveSettingsSyncPage_BackingUp"));
        try
        {
            var profilePath = SelectedProfile.Path;
            var (backup, error) = await Task.Run(() =>
            {
                var item = EveSettingsBackupService.CreateFolderBackup(profilePath, name, display, out var backupError);
                return (item, backupError);
            });

            if (backup is null)
            {
                PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_BackupFailed"), error ?? string.Empty));
                return null;
            }

            PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_BackupDone"), backup.Name, backup.FileCount));
            return backup;
        }
        finally
        {
            PageNotifyService.HideWaiting();
            await LoadBackupsAsync();
        }
    }

    /// <summary>备份选中的一个或多个设置文件（每个文件一个备份项，名字带时间戳、自动避重名）。</summary>
    public async Task<int> BackupFilesAsync(IReadOnlyList<EveSettingsFileItem> files)
    {
        if (files.Count == 0)
        {
            return 0;
        }

        var serverDir = ServerDirName;
        var profileName = ProfileName;
        var serverDisplay = SelectedServer?.DisplayName ?? serverDir;

        PageNotifyService.ShowWaiting(FindString("EveSettingsSyncPage_BackingUp"));
        try
        {
            var (created, failures) = await Task.Run(() =>
            {
                var ok = 0;
                var fails = new List<string>();

                foreach (var file in files)
                {
                    var name = EveSettingsBackupService.EnsureUniqueName(
                        EveSettingsBackupService.BuildFileBackupName(serverDir, profileName, file.FileName));
                    var display = $"{serverDisplay} / {profileName} / {file.FileName}";

                    var backup = EveSettingsBackupService.CreateFileBackup(file.Path, name, display, out var error);
                    if (backup is null)
                    {
                        fails.Add($"{file.FileName}: {error}");
                    }
                    else
                    {
                        ok++;
                    }
                }

                return (ok, fails);
            });

            if (created == 0)
            {
                PageNotifyService.Error(string.Format(
                    FindString("EveSettingsSyncPage_BackupFailed"),
                    failures.FirstOrDefault() ?? string.Empty));
            }
            else if (failures.Count > 0)
            {
                PageNotifyService.Warning(string.Format(FindString("EveSettingsSyncPage_BackupPartial"), created, failures.Count));
            }
            else
            {
                PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_BackupFilesDone"), created));
            }

            return created;
        }
        finally
        {
            PageNotifyService.HideWaiting();
            await LoadBackupsAsync();
        }
    }

    /// <summary>把备份还原到当前配置文件（开启自动备份时先备份当前状态）。</summary>
    public async Task<EveSettingsRestoreResult?> RestoreBackupAsync(EveSettingsBackupItem backup)
    {
        if (SelectedProfile is null)
        {
            return null;
        }

        string? autoBackupName = null;
        if (AutoBackupBeforeSync)
        {
            var created = await BackupProfileAsync();
            if (created is null)
            {
                return null; // 备份失败就不动用户数据
            }

            autoBackupName = created.Name;
        }

        var profilePath = SelectedProfile.Path;
        PageNotifyService.ShowWaiting(FindString("EveSettingsSyncPage_Restoring"));

        EveSettingsRestoreResult result;
        try
        {
            result = await Task.Run(() => EveSettingsBackupService.Restore(backup, profilePath));
        }
        finally
        {
            PageNotifyService.HideWaiting();
        }

        if (result.Error is not null)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_RestoreFailed"), result.Error));
        }
        else if (result.Failed > 0)
        {
            PageNotifyService.Warning(string.Format(
                FindString("EveSettingsSyncPage_RestoreDone"),
                result.Copied,
                result.Failed,
                result.ExtraInProfile));
        }
        else
        {
            var text = string.Format(FindString("EveSettingsSyncPage_RestoreDone"), result.Copied, 0, result.ExtraInProfile);
            if (autoBackupName is not null)
            {
                text += " " + string.Format(FindString("EveSettingsSyncPage_BackupSuffix"), autoBackupName);
            }

            PageNotifyService.Success(text);
        }

        if (SelectedProfile is not null)
        {
            await SelectProfileAsync(SelectedProfile);
        }

        await LoadBackupsAsync();
        return result;
    }

    public async Task<bool> DeleteBackupAsync(EveSettingsBackupItem backup)
    {
        var (ok, error) = await Task.Run(() =>
        {
            var success = EveSettingsBackupService.Delete(backup, out var deleteError);
            return (success, deleteError);
        });

        if (!ok)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), error ?? string.Empty));
            return false;
        }

        PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_BackupDeleted"), backup.Name));
        await LoadBackupsAsync();
        return true;
    }

    // ---------- 配置文件管理 ----------

    /// <summary>校验配置文件名称（页面据此给本地化提示）。</summary>
    public EveSettingsProfileNameError ValidateProfileName(string? name, bool allowExisting = false)
        => SelectedServer is null
            ? EveSettingsProfileNameError.Empty
            : EveSettingsFolderService.ValidateProfileName(name, SelectedServer.Path, allowExisting);

    public async Task<bool> CreateProfileAsync(string name)
    {
        if (SelectedServer is null)
        {
            return false;
        }

        var serverPath = SelectedServer.Path;
        var (profile, error) = await Task.Run(() =>
        {
            var item = EveSettingsFolderService.CreateProfile(serverPath, name, out var createError);
            return (item, createError);
        });

        if (profile is null)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), error ?? string.Empty));
            return false;
        }

        await ReloadProfilesAsync(profile.Name);
        PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_ProfileCreated"), profile.Name));
        return true;
    }

    public async Task<bool> RenameProfileAsync(string newName)
    {
        if (SelectedServer is null || SelectedProfile is null)
        {
            return false;
        }

        var serverPath = SelectedServer.Path;
        var oldName = SelectedProfile.Name;
        var (profile, error) = await Task.Run(() =>
        {
            var item = EveSettingsFolderService.RenameProfile(serverPath, oldName, newName, out var renameError);
            return (item, renameError);
        });

        if (profile is null)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), error ?? string.Empty));
            return false;
        }

        await ReloadProfilesAsync(profile.Name);
        PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_ProfileRenamed"), profile.Name));
        return true;
    }

    public async Task<bool> DuplicateProfileAsync(string newName)
    {
        if (SelectedServer is null || SelectedProfile is null)
        {
            return false;
        }

        var serverPath = SelectedServer.Path;
        var sourceName = SelectedProfile.Name;
        var (profile, error) = await Task.Run(() =>
        {
            var item = EveSettingsFolderService.DuplicateProfile(serverPath, sourceName, newName, out var duplicateError);
            return (item, duplicateError);
        });

        if (profile is null)
        {
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), error ?? string.Empty));
            return false;
        }

        if (!string.IsNullOrEmpty(error))
        {
            PageNotifyService.Warning(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), error));
        }
        else
        {
            PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_ProfileDuplicated"), profile.Name));
        }

        await ReloadProfilesAsync(profile.Name);
        return true;
    }

    public async Task<bool> DeleteProfileAsync()
    {
        if (SelectedServer is null || SelectedProfile is null)
        {
            return false;
        }

        if (Profiles.Count <= 1)
        {
            PageNotifyService.Warning(FindString("EveSettingsSyncPage_ProfileOnlyOne"));
            return false;
        }

        var serverPath = SelectedServer.Path;
        var profileName = SelectedProfile.Name;
        var (ok, error) = await Task.Run(() =>
        {
            var success = EveSettingsFolderService.DeleteProfile(serverPath, profileName, out var deleteError);
            return (success, deleteError);
        });

        if (!ok)
        {
            var detail = error == "only-one-profile" ? FindString("EveSettingsSyncPage_ProfileOnlyOne") : error ?? string.Empty;
            PageNotifyService.Error(string.Format(FindString("EveSettingsSyncPage_OperationFailed"), detail));
            return false;
        }

        await ReloadProfilesAsync(null);
        PageNotifyService.Success(string.Format(FindString("EveSettingsSyncPage_ProfileDeleted"), profileName));
        return true;
    }

    /// <summary>重新枚举配置文件并选中指定项（无则保持/退回第一个）。</summary>
    private async Task ReloadProfilesAsync(string? selectName)
    {
        if (SelectedServer is null)
        {
            return;
        }

        var wanted = selectName ?? SelectedProfile?.Name;
        var serverPath = SelectedServer.Path;

        Profiles.Clear();
        foreach (var profile in EveSettingsFolderService.GetProfiles(serverPath))
        {
            Profiles.Add(profile);
        }

        var target = Profiles.FirstOrDefault(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase))
                     ?? Profiles.FirstOrDefault();

        await SelectProfileAsync(target);
        UpdateStates();
    }

    // ---------- 游戏运行检测 ----------

    /// <summary>
    /// 检测 EVE 客户端是否在运行（复用「多开」模块的进程发现，关键词取本模块设置、
    /// 缺省回落到「多开」的 <c>ProcessKeywords</c> = exefile）。
    /// </summary>
    public void RefreshGameState()
    {
        var keywords = EveSettingsSettingService.Value.ProcessKeywords;
        if (string.IsNullOrWhiteSpace(keywords))
        {
            keywords = GamePreviewSettingService.Current.Value.ProcessKeywords;
        }

        var names = new List<string>();
        var clients = GameClientService.FindClients(keywords);
        try
        {
            foreach (var client in clients)
            {
                var name = client.GetCharacterName();
                names.Add(string.IsNullOrWhiteSpace(name) ? client.ProcessName : name!);
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                try
                {
                    client.Process?.Dispose();
                }
                catch (Exception ex)
                {
                    Core.Log.Warn($"[配置同步] 释放进程句柄失败：{ex.Message}");
                }
            }
        }

        IsGameRunning = names.Count > 0;
        RunningGameText = names.Count == 0
            ? string.Empty
            : string.Format(
                FindString("EveSettingsSyncPage_GameRunning"),
                names.Count,
                string.Join("、", names.Take(5)));
    }

    // ---------- 内部 ----------

    private void ClearFiles()
    {
        CharacterFiles.Clear();
        AccountFiles.Clear();
        UnrecognizedFiles.Clear();
        StatusText = string.Empty;
        UpdateStates();
    }

    private void UpdateStates()
    {
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasCharacterFiles));
        OnPropertyChanged(nameof(HasAccountFiles));
        OnPropertyChanged(nameof(HasUnrecognizedFiles));
        OnPropertyChanged(nameof(UnrecognizedCount));
        OnPropertyChanged(nameof(CanManageProfiles));
        OnPropertyChanged(nameof(CanManageFiles));
        OnPropertyChanged(nameof(CanDeleteProfile));
    }

    private static EveSettingsServerKind CurrentGameServerKind()
        => GameServerSelectorService.Value == Core.Enums.GameServerType.Serenity
            ? EveSettingsServerKind.Serenity
            : EveSettingsServerKind.Tranquility;

    /// <summary>备份列表里的"来源"文案（服务器 / 配置文件）。</summary>
    private string BuildBackupDisplayName()
        => $"{SelectedServer?.DisplayName ?? ServerDirName} / {ProfileName}";

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

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
