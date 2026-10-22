namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>
/// 不依赖 ESI 的自定义工业角色：只保存名称与工业技能等级，
/// 用于在没有登录角色时按同一套公式估算槽位与耗时。
/// </summary>
public sealed class IndustryCustomCharacter
{
    public long Id { get; set; }
    public string Name { get; set; } = "自定义角色";

    public int MassProduction { get; set; }
    public int AdvancedMassProduction { get; set; }
    public int LaboratoryOperation { get; set; }
    public int AdvancedLaboratoryOperation { get; set; }
    public int MassReactions { get; set; }
    public int AdvancedMassReactions { get; set; }
    public int Industry { get; set; }
    public int AdvancedIndustry { get; set; }
    public int Reactions { get; set; }
    public int AdvancedReactions { get; set; }

    public IndustryCustomCharacter Clone() => (IndustryCustomCharacter)MemberwiseClone();

    /// <summary>新建角色默认全部工业技能 5 级，用户只需改有差异的等级。</summary>
    public static IndustryCustomCharacter CreateDefault(long id) => new()
    {
        Id = id,
        Name = "自定义角色",
        MassProduction = 5,
        AdvancedMassProduction = 5,
        LaboratoryOperation = 5,
        AdvancedLaboratoryOperation = 5,
        MassReactions = 5,
        AdvancedMassReactions = 5,
        Industry = 5,
        AdvancedIndustry = 5,
        Reactions = 5,
        AdvancedReactions = 5,
    };
}
