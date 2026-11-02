using System;
using System.Collections.Generic;
using System.Linq;

namespace TheGuideToTheNewEden.WPF.Services.Update;

/// <summary>
/// 语义化版本（SemVer 2.0）的最小实现：只用于"当前版本 vs 发布版本"的比较。
/// </summary>
/// <remarks>
/// 为什么不用 <see cref="Version"/>：项目的版本号带预发布后缀（<c>4.0.0-alpha.2</c>），
/// <see cref="Version.TryParse"/> 只认 <c>4.0.0.2</c> 这种四段数字，
/// 于是 <c>4.0.0-alpha.2</c> 会被截成 <c>4.0.0</c>——正式版 <c>4.0.0</c> 发布后，
/// alpha 用户永远被判成"已是最新"（阶段 262 之前的实际行为）。
/// 这里按 SemVer 规则比较：<c>4.0.0 &gt; 4.0.0-rc.1 &gt; 4.0.0-beta.2 &gt; 4.0.0-alpha.10 &gt; 4.0.0-alpha.2</c>。
/// </remarks>
public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _preRelease;

    private SemanticVersion(int major, int minor, int patch, string[] preRelease, string original)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
        Original = original;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>原始文本（去掉前导 v/V，保留预发布后缀），用于显示与日志。</summary>
    public string Original { get; }

    public bool IsPreRelease => _preRelease.Length > 0;

    /// <summary>解析 <c>v4.0.0</c> / <c>4.0.0-alpha.2</c>；无法解析时返回 false。</summary>
    public static bool TryParse(string? text, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var original = text.Trim();
        if (original.StartsWith('v') || original.StartsWith('V'))
        {
            original = original[1..];
        }

        // 构建元数据（+abc123）不参与优先级比较
        var plus = original.IndexOf('+');
        if (plus >= 0)
        {
            original = original[..plus];
        }

        var dash = original.IndexOf('-');
        var core = dash >= 0 ? original[..dash] : original;
        var preReleaseText = dash >= 0 ? original[(dash + 1)..] : string.Empty;

        var parts = core.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var value) || value < 0)
            {
                return false;
            }

            numbers[i] = value;
        }

        var preRelease = preReleaseText.Length == 0
            ? Array.Empty<string>()
            : preReleaseText.Split('.', StringSplitOptions.RemoveEmptyEntries);

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], preRelease, original);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        if (core == 0)
        {
            core = Minor.CompareTo(other.Minor);
        }

        if (core == 0)
        {
            core = Patch.CompareTo(other.Patch);
        }

        if (core != 0)
        {
            return core;
        }

        // 有预发布后缀的版本低于同号正式版
        if (!IsPreRelease || !other.IsPreRelease)
        {
            return (other.IsPreRelease ? 1 : 0) - (IsPreRelease ? 1 : 0);
        }

        return ComparePreRelease(_preRelease, other._preRelease);
    }

    private static int ComparePreRelease(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var length = Math.Min(left.Count, right.Count);
        for (var i = 0; i < length; i++)
        {
            var a = left[i];
            var b = right[i];

            var aNumeric = int.TryParse(a, out var aNumber);
            var bNumeric = int.TryParse(b, out var bNumber);

            if (aNumeric && bNumeric)
            {
                var compare = aNumber.CompareTo(bNumber);
                if (compare != 0)
                {
                    return compare;
                }
            }
            else if (aNumeric != bNumeric)
            {
                // 数字标识符低于字母标识符（SemVer 2.0 §11.4.3）
                return aNumeric ? -1 : 1;
            }
            else
            {
                var compare = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                if (compare != 0)
                {
                    return compare;
                }
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    public override string ToString() => Original;

    /// <summary><paramref name="candidate"/> 是否严格新于 <paramref name="current"/>。</summary>
    public static bool IsNewer(string candidate, string current)
        => TryParse(candidate, out var candidateVersion)
           && TryParse(current, out var currentVersion)
           && candidateVersion!.CompareTo(currentVersion) > 0;
}
