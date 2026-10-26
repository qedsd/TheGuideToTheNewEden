using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>
/// 从角色技能推导工业槽位与时间修正：
/// 制造 = 1 + Mass Production + Advanced Mass Production（1–11），
/// 发明 = 1 + Laboratory Operation + Advanced Laboratory Operation，
/// 反应 = 1 + Mass Reactions + Advanced Mass Reactions。
/// 时间修正取 Industry / Advanced Industry（制造）与 Reactions / Advanced Reactions（反应）。
/// </summary>
public static class IndustryCharacterProfileFactory
{
    private static readonly Dictionary<string, int> SkillTypeIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Sync = new();

    private static readonly string[] SkillNames =
    {
        "Mass Production", "Advanced Mass Production",
        "Laboratory Operation", "Advanced Laboratory Operation",
        "Mass Reactions", "Advanced Mass Reactions",
        "Industry", "Advanced Industry",
        "Reactions", "Advanced Reactions",
    };

    public static IndustryCharacterProfile Create(CharacterContext? context, Dictionary<int, int>? skills)
    {
        if (context is null)
        {
            return CreateDefault();
        }

        return Create(context.CharacterId, context.Name, skills);
    }

    public static IndustryCharacterProfile Create(long characterId, string characterName, Dictionary<int, int>? skills)
    {
        var profile = new IndustryCharacterProfile
        {
            CharacterId = characterId,
            CharacterName = characterName,
            Source = "skills",
        };

        if (skills is null)
        {
            profile.ManufacturingSlots = 1;
            profile.InventionSlots = 1;
            profile.ReactionSlots = 1;
            profile.Source = "skillsUnread";
            return profile;
        }

        int Level(string name)
        {
            var id = ResolveSkillId(name);
            return id > 0 && skills.TryGetValue(id, out var level) ? Math.Clamp(level, 0, 5) : 0;
        }

        profile.ManufacturingSlots = ClampSlots(1 + Level("Mass Production") + Level("Advanced Mass Production"));
        profile.InventionSlots = ClampSlots(1 + Level("Laboratory Operation") + Level("Advanced Laboratory Operation"));
        profile.ReactionSlots = ClampSlots(1 + Level("Mass Reactions") + Level("Advanced Mass Reactions"));
        profile.ManufacturingTimeMultiplier = TimeMultiplier(Level("Industry"), Level("Advanced Industry"));
        profile.ReactionTimeMultiplier = TimeMultiplier(Level("Reactions"), Level("Advanced Reactions"));
        profile.InventionTimeMultiplier = 1.0;
        profile.SkillsLoaded = true;
        return profile;
    }

    public static IndustryCharacterProfile CreateDefault()
    {
        return new IndustryCharacterProfile
        {
            CharacterId = 0,
            CharacterName = "auto:default",
            ManufacturingSlots = 11,
            InventionSlots = 11,
            ReactionSlots = 11,
            Source = "default",
            SkillsLoaded = false,
        };
    }

    public static IndustryCharacterProfile CreateCustom(IndustryCustomCharacter character)
    {
        var name = string.IsNullOrWhiteSpace(character?.Name)
            ? LanguageService.Get("IndustryCustomCharacter_DefaultName")
            : character!.Name.Trim();
        var profile = new IndustryCharacterProfile
        {
            CharacterId = character?.Id ?? 0,
            CharacterName = name,
            Source = "custom",
            SkillsLoaded = true,
        };

        if (character is null)
        {
            return profile;
        }

        profile.ManufacturingSlots = ClampSlots(1 + ClampLevel(character.MassProduction) + ClampLevel(character.AdvancedMassProduction));
        profile.InventionSlots = ClampSlots(1 + ClampLevel(character.LaboratoryOperation) + ClampLevel(character.AdvancedLaboratoryOperation));
        profile.ReactionSlots = ClampSlots(1 + ClampLevel(character.MassReactions) + ClampLevel(character.AdvancedMassReactions));
        profile.ManufacturingTimeMultiplier = TimeMultiplier(ClampLevel(character.Industry), ClampLevel(character.AdvancedIndustry));
        profile.ReactionTimeMultiplier = TimeMultiplier(ClampLevel(character.Reactions), ClampLevel(character.AdvancedReactions));
        profile.InventionTimeMultiplier = 1.0;
        return profile;
    }

    private static int ClampLevel(int value) => Math.Clamp(value, 0, 5);

    private static int ClampSlots(int value) => Math.Clamp(value, 1, 11);

    private static double TimeMultiplier(int primaryLevel, int advancedLevel)
    {
        var value = (1.0 - 0.04 * primaryLevel) * (1.0 - 0.03 * advancedLevel);
        return Math.Clamp(value, 0.5, 1.0);
    }

    private static int ResolveSkillId(string name)
    {
        lock (Sync)
        {
            if (SkillTypeIds.TryGetValue(name, out var cached))
            {
                return cached;
            }

            try
            {
                var matches = InvTypeService.QueryByName(name, false);
                // QueryByName 会按主库英文名精确查询，但返回后可能被本地化库替换 TypeName；
                // 因此这里只取查询结果，不能再用英文名比对返回的 TypeName。
                SkillTypeIds[name] = matches?.FirstOrDefault()?.TypeID ?? 0;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                SkillTypeIds[name] = 0;
            }

            return SkillTypeIds[name];
        }
    }
}
