using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TheGuideToTheNewEden.WPF.Models.EveSettings;

/// <summary>
/// EVE 客户端设置文件所属的服务器。
/// <para>
/// <b>按目录名判定</b>（见 <c>EveSettingsServerCatalog</c>），与"本应用当前选的是欧服/国服"
/// 无关——用户完全可能一边开着国际服授权、一边整理国服目录。
/// </para>
/// </summary>
public enum EveSettingsServerKind
{
    Tranquility,
    Serenity,
    Infinity,

    /// <summary>奇点 / Duality / 雷霆穹顶等没有公开 ESI 的服务器（不做角色名解析）。</summary>
    Other,
}

/// <summary>设置文件的类型（由文件名判定）。</summary>
public enum EveSettingsFileKind
{
    /// <summary><c>core_char_&lt;角色ID&gt;.dat</c>：角色级设置（总览、按键、UI 布局…）。</summary>
    Character,

    /// <summary><c>core_user_&lt;账户ID&gt;.dat</c>：账户级设置。</summary>
    Account,

    /// <summary>
    /// 命名不符合上述两种规则的文件（实机见过 <c>core_char__.dat</c>、
    /// <c>core_char_('char', None, 'dat').dat</c> 这类脏数据）。
    /// <b>可见、可备份，但不作为同步的源或目标</b>——避免把设置写进一个来路不明的文件。
    /// </summary>
    Unrecognized,
}

/// <summary>EVE 设置根目录（<c>%LOCALAPPDATA%\CCP\EVE</c>）下的一级服务器目录。</summary>
public sealed class EveSettingsServer
{
    /// <summary>目录名，如 <c>c_ccp_eve_tq_tranquility</c>（设置里按它记忆上次选择）。</summary>
    public string DirName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    /// <summary>显示名，如 <c>Tranquility</c>；带语言后缀的目录会得到 <c>Serenity (Chinese)</c>。</summary>
    public string DisplayName { get; init; } = string.Empty;

    public EveSettingsServerKind Kind { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>配置文件：服务器目录下的一个 <c>settings_&lt;名称&gt;</c> 目录。</summary>
public sealed class EveSettingsProfile
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>
/// 一个设置文件（<c>.dat</c>）。
/// 角色名是**异步**解析出来的，所以实现 INotifyPropertyChanged 让列表就地刷新。
/// </summary>
public sealed class EveSettingsFileItem : INotifyPropertyChanged
{
    public string FileName { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public EveSettingsFileKind Kind { get; init; }

    /// <summary>角色 ID / 账户 ID；<see cref="EveSettingsFileKind.Unrecognized"/> 时为 0。</summary>
    public long Id { get; init; }

    public long Size { get; init; }

    public DateTime ModifiedAt { get; init; }

    private string? _resolvedName;

    /// <summary>角色名（仅角色文件、且解析成功时有值）。</summary>
    public string? ResolvedName
    {
        get => _resolvedName;
        set
        {
            if (string.Equals(_resolvedName, value, StringComparison.Ordinal))
            {
                return;
            }

            _resolvedName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>列表里显示的名称：角色名 → ID（未识别文件则显示原始文件名）。</summary>
    public string DisplayName
        => !string.IsNullOrEmpty(_resolvedName)
            ? _resolvedName!
            : Kind == EveSettingsFileKind.Unrecognized
                ? FileName
                : $"ID {Id}";

    public string SizeText => EveSettingsFormat.FormatSize(Size);

    public string ModifiedText => ModifiedAt.ToString("yyyy-MM-dd HH:mm:ss");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>备份仓库里的一项：整配置文件备份（目录）或单个文件备份（.dat）。</summary>
public sealed class EveSettingsBackupItem
{
    /// <summary>备份名（不含扩展名；目录备份即目录名）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>目录备份为目录路径，文件备份为 .dat 文件路径。</summary>
    public string Path { get; init; } = string.Empty;

    public bool IsFolder { get; init; }

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>来源说明（如 <c>Tranquility / Default</c> 或 <c>Tranquility / Default / core_char_2112494169.dat</c>）。</summary>
    public string? DisplayName { get; init; }

    /// <summary>来源说明（没有时退回备份名）。</summary>
    public string SourceText => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName!;

    public string SizeText => EveSettingsFormat.FormatSize(TotalBytes);

    public string CreatedText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>一次同步的结果（页面据此提示与展示失败明细）。</summary>
public sealed class EveSettingsSyncResult
{
    public int Succeeded { get; set; }

    public int Failed { get; set; }

    /// <summary>同步前自动创建的整配置文件备份名（未开启自动备份时为 null）。</summary>
    public string? AutoBackupName { get; set; }

    /// <summary>整体失败原因（源文件不存在、自动备份失败等）。</summary>
    public string? Error { get; set; }

    public bool Cancelled { get; set; }

    public List<(string FileName, string Error)> Failures { get; set; } = [];

    public bool HasFailureDetail => Failed > 0 || Error is not null;
}

/// <summary>一次还原的结果。</summary>
public sealed class EveSettingsRestoreResult
{
    public int Copied { get; set; }

    public int Failed { get; set; }

    /// <summary>当前配置文件里"备份中没有"的文件数（<b>不删除</b>，仅提示）。</summary>
    public int ExtraInProfile { get; set; }

    public string? Error { get; set; }

    public List<(string FileName, string Error)> Failures { get; set; } = [];
}

/// <summary>配置文件名称校验结果（文案由页面映射，服务层不认识语言键）。</summary>
public enum EveSettingsProfileNameError
{
    None,
    Empty,
    InvalidChars,
    Exists,
}

/// <summary>格式化小工具（列表里的体积显示）。</summary>
public static class EveSettingsFormat
{
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }
}
