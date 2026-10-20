using System.Globalization;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 用真实 usage 校准 token 估算（阶段 151）。
/// <para>
/// <see cref="AssistantTokenEstimator"/> 是"宁可高估"的经验公式（CJK 0.85 token/字、其余 3.2 字符/token），
/// 对实际分词器往往偏高——高估只会少带几轮历史，但白白浪费上下文。这里把每次请求的
/// **实际输入 token** 与**发送前估算**做比，滚动学习一个系数（EMA），再回灌给估算器：
/// 高估时系数 &lt; 1（能多带几轮），低估时 &gt; 1（更保守）。系数夹在 0.6–1.5，避免单次异常把预算带偏。
/// </para>
/// <para>
/// **样本门槛**：只认输入 ≥ <see cref="MinSampleInputTokens"/> 的请求——探针的 mock 服务只回 11/20/30 token，
/// 因此自动跳过，不会污染探针里的精确估算断言。
/// </para>
/// <para>按模型名分开记（同一端点换模型不会互相污染），落盘在共享 <c>settings.json</c> 的
/// <c>Assistant.TokenCalibration</c>（每行 <c>模型=系数</c>）。</para>
/// </summary>
public static class AssistantUsageCalibration
{
    private const string SettingsKey = "Assistant.TokenCalibration";
    private const string DefaultModelKey = "(default)";

    /// <summary>一次请求至少要有这么多实际输入 token 才参与校准（挡住 mock / 极短请求）。</summary>
    public const int MinSampleInputTokens = 200;

    /// <summary>EMA 权重：新样本占 30%（系数变化平滑，不被单次波动带跑）。</summary>
    public const double Alpha = 0.3;

    private static readonly Dictionary<string, double> Factors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>是否启用学习（探针可关掉；生产默认开）。</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>当前已知的模型 → 系数（诊断/展示用）。</summary>
    public static IReadOnlyDictionary<string, double> Snapshot => Factors;

    /// <summary>从 settings.json 载入（由 <c>AiAssistantSettingService.Initialize</c> 调用；幂等）。</summary>
    public static void Load()
    {
        Factors.Clear();
        var raw = SettingsService.GetValue(SettingsKey) ?? string.Empty;
        foreach (var line in raw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.LastIndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var model = line[..separator].Trim();
            if (model.Length == 0
                || !double.TryParse(line[(separator + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var factor))
            {
                continue;
            }

            Factors[model] = Math.Clamp(factor, AssistantTokenEstimator.MinCalibrationFactor, AssistantTokenEstimator.MaxCalibrationFactor);
        }
    }

    /// <summary>把该模型的系数套到估算器上（每次发送前调用）。</summary>
    public static void ApplyToEstimator(string? model)
        => AssistantTokenEstimator.SetCalibrationFactor(Get(model));

    /// <summary>取某个模型当前的校准系数（未知模型 = 1.0）。</summary>
    public static double Get(string? model)
        => Factors.TryGetValue(Normalize(model), out var factor) ? factor : 1.0;

    /// <summary>
    /// 纯函数：给定当前系数、估算输入、实际输入，算下一个系数（EMA + 两个方向都夹紧）。
    /// 供探针离线断言，不读写设置。
    /// </summary>
    public static double ComputeFactor(double current, int estimatedInputTokens, int actualInputTokens)
    {
        var clampedCurrent = Math.Clamp(current, AssistantTokenEstimator.MinCalibrationFactor, AssistantTokenEstimator.MaxCalibrationFactor);
        if (estimatedInputTokens <= 0 || actualInputTokens <= 0)
        {
            return clampedCurrent;
        }

        var ratio = (double)actualInputTokens / estimatedInputTokens;
        ratio = Math.Clamp(ratio, AssistantTokenEstimator.MinCalibrationFactor, AssistantTokenEstimator.MaxCalibrationFactor);
        var next = clampedCurrent * (1 - Alpha) + ratio * Alpha;
        return Math.Clamp(next, AssistantTokenEstimator.MinCalibrationFactor, AssistantTokenEstimator.MaxCalibrationFactor);
    }

    /// <summary>
    /// 记录一次真实用量。样本太小（mock / 极短请求）、未启用、或数据异常时直接忽略。
    /// </summary>
    public static void Record(string? model, int estimatedInputTokens, int actualInputTokens)
    {
        if (!Enabled
            || estimatedInputTokens < MinSampleInputTokens
            || actualInputTokens < MinSampleInputTokens)
        {
            return;
        }

        var key = Normalize(model);
        var next = ComputeFactor(Get(model), estimatedInputTokens, actualInputTokens);
        Factors[key] = next;
        Persist();
    }

    /// <summary>清空学习结果并把估算器复位到 1.0（"恢复默认"用）。</summary>
    public static void Reset()
    {
        Factors.Clear();
        AssistantTokenEstimator.ResetCalibration();
        SettingsService.SetValue(SettingsKey, string.Empty);
        SettingsService.Save();
    }

    private static string Normalize(string? model)
        => string.IsNullOrWhiteSpace(model) ? DefaultModelKey : model.Trim();

    private static void Persist()
    {
        var text = string.Join(
            "\n",
            Factors
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value.ToString("0.####", CultureInfo.InvariantCulture)}"));

        SettingsService.SetValue(SettingsKey, text);
        SettingsService.Save();
    }
}