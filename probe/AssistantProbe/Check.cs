namespace TheGuideToTheNewEden.Probe;

/// <summary>极简断言器：计数 PASS/FAIL、打印每条断言、收集失败原因。</summary>
internal sealed class Check
{
    private readonly List<string> _failures = [];

    public int Pass { get; private set; }

    public int Fail { get; private set; }

    public IReadOnlyList<string> Failures => _failures;

    public void Section(string title) => Console.WriteLine($"\n===== {title} =====");

    public void That(bool condition, string message)
    {
        if (condition)
        {
            Pass++;
            Console.WriteLine($"  PASS  {message}");
        }
        else
        {
            Fail++;
            _failures.Add(message);
            Console.WriteLine($"  FAIL  {message}");
        }
    }

    public void Equal<T>(T actual, T expected, string message)
        => That(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}（期望 {Describe(expected)}，实际 {Describe(actual)}）");

    public void NotNull(object? value, string message) => That(value is not null, message);

    /// <summary>区间断言（数值可能不是整数）。</summary>
    public void Between(double actual, double min, double max, string message)
        => That(actual >= min && actual <= max, $"{message}（期望 {min}–{max}，实际 {actual}）");

    private static string Describe<T>(T? value) => value switch
    {
        null => "null",
        string s => s.Length <= 60 ? $"\"{s}\"" : $"\"{s[..57]}…\"",
        _ => value.ToString() ?? "?",
    };
}
