using System.IO;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>ESI 权限范围（scope）集合与用户勾选结果。</summary>
public sealed class ESIScopeService
{
    private static ESIScopeService? _current;
    public static ESIScopeService Current => _current ??= new ESIScopeService();

    private static readonly string SourceFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "ESIScopes.txt");

    private static readonly string SelectedFilePath = Path.Combine(
        SettingsService.DataPath, "Configs", "ESIScopes.txt");

    private List<string>? _allScopes;

    public List<string> GetAllScopes()
    {
        if (_allScopes is not null)
        {
            return _allScopes;
        }

        _allScopes = File.Exists(SourceFilePath)
            ? File.ReadAllLines(SourceFilePath).ToList()
            : [];

        // 国服不支持该权限。
        if (GameServerSelectorService.Value == GameServerType.Serenity)
        {
            _allScopes.Remove("esi-corporations.read_projects.v1");
        }

        return _allScopes;
    }

    public List<string> GetSelectedScopes()
    {
        return File.Exists(SelectedFilePath)
            ? File.ReadAllLines(SelectedFilePath).ToList()
            : GetAllScopes();
    }

    public void SelectScope(string scope)
    {
        var selected = GetSelectedScopes();
        if (!selected.Contains(scope))
        {
            selected.Add(scope);
            Write(selected);
        }
    }

    public void SelectAllScope()
    {
        Write(GetAllScopes());
    }

    public void CancelSelectScope(string scope)
    {
        var selected = GetSelectedScopes();
        if (selected.Remove(scope))
        {
            Write(selected);
        }
    }

    public void CancelSelectAllScope()
    {
        Write([]);
    }

    private static void Write(IEnumerable<string> scopes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SelectedFilePath)!);
        File.WriteAllLines(SelectedFilePath, scopes);
    }
}