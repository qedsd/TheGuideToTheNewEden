using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Character;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// 已授权角色的令牌存储：按游戏服务器分别落盘（Auth.json / Auth_Serenity.json）。
/// </summary>
public static class CharacterStore
{
    private static readonly string TranquilityFile = Path.Combine(SettingsService.DataPath, "Configs", "Auth.json");
    private static readonly string SerenityFile = Path.Combine(SettingsService.DataPath, "Configs", "Auth_Serenity.json");

    public static ObservableCollection<AuthorizedCharacterData> Characters { get; private set; } = [];

    public static event EventHandler? Changed;

    private static string CurrentFile =>
        GameServerSelectorService.Value == GameServerType.Tranquility ? TranquilityFile : SerenityFile;

    public static void Init()
    {
        Characters = Read(CurrentFile);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Save()
    {
        Write(CurrentFile, Characters);
    }

    public static void Add(AuthorizedCharacterData character)
    {
        if (Characters.All(p => p.CharacterID != character.CharacterID))
        {
            Characters.Add(character);
        }

        Save();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Remove(AuthorizedCharacterData character)
    {
        if (Characters.Remove(character))
        {
            Save();
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>移动角色顺序并持久化。</summary>
    public static void Move(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex
            || oldIndex < 0 || newIndex < 0
            || oldIndex >= Characters.Count || newIndex >= Characters.Count)
        {
            return;
        }

        Characters.Move(oldIndex, newIndex);
        Save();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static AuthorizedCharacterData? Get(long characterId)
    {
        return Characters.FirstOrDefault(p => p.CharacterID == characterId);
    }

    /// <summary>确保令牌可用（过期则刷新）。所有需要授权的调用都应先经过这里。</summary>
    public static async Task<bool> EnsureTokenValidAsync(AuthorizedCharacterData character)
    {
        if (character.IsTokenValid())
        {
            return true;
        }

        if (await character.RefreshTokenAsync())
        {
            Save();
            return true;
        }

        Core.Log.Error($"角色 {character.CharacterName} 的令牌刷新失败");
        return false;
    }

    public static async Task<AuthorizedCharacterData?> GetDefaultAsync()
    {
        var character = Characters.FirstOrDefault();
        if (character is null)
        {
            return null;
        }

        return await EnsureTokenValidAsync(character) ? character : null;
    }

    private static ObservableCollection<AuthorizedCharacterData> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var content = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ObservableCollection<AuthorizedCharacterData>>(content) ?? [];
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    private static void Write(string path, ObservableCollection<AuthorizedCharacterData> characters)
    {
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(characters));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}