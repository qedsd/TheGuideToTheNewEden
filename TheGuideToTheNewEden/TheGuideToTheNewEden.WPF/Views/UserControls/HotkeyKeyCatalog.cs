using System.IO;
using System.Runtime.InteropServices;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>快捷键选择器里的一个"主键"选项。</summary>
public sealed record HotkeyKeyOption(string Label, string Key, int Code, string? Note = null)
{
    public string Display => string.IsNullOrWhiteSpace(Note) ? Label : $"{Label}  ·  {Note}";

    public override string ToString() => Display;
}

/// <summary>
/// 快捷键键表 + 模拟键盘布局的共享定义。
/// <para>
/// 键盘 UI（<c>HotkeyPickerWindow</c>）和捕获逻辑共用这一份，避免"列表里有、键盘上没有"
/// 或"键盘能点、解析器不认"的两套表。符号键使用友好名（<c>-</c> / <c>[</c> 等），
/// 由 <c>GlobalHotkeyService</c> 的别名映射到 VK_OEM_*。
/// </para>
/// </summary>
public static class HotkeyKeyCatalog
{
    public static readonly string[] ModifierOrder = ["Ctrl", "Alt", "Shift", "Win"];

    public static readonly HashSet<string> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shift", "Ctrl", "Control", "Alt", "Win", "Windows",
        "LeftWin", "RightWin", "LShift", "RShift", "LControl", "RControl", "LMenu", "RMenu",
    };

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    /// <summary>当前物理按住的修饰键（用于"直接按键录制"，不依赖事件顺序）。</summary>
    public static IReadOnlyList<string> GetCurrentModifiers()
    {
        var modifiers = new List<string>();
        if (IsDown(VkControl) || IsDown(VkLControl) || IsDown(VkRControl))
        {
            modifiers.Add("Ctrl");
        }

        if (IsDown(VkMenu) || IsDown(VkLMenu) || IsDown(VkRMenu))
        {
            modifiers.Add("Alt");
        }

        if (IsDown(VkShift) || IsDown(VkLShift) || IsDown(VkRShift))
        {
            modifiers.Add("Shift");
        }

        if (IsDown(VkLWin) || IsDown(VkRWin))
        {
            modifiers.Add("Win");
        }

        return modifiers;
    }

    /// <summary>虚拟键码是不是修饰键；是则返回统一名称（Ctrl/Alt/Shift/Win）。</summary>
    public static string? GetModifierName(int virtualKey) => virtualKey switch
    {
        VkControl or VkLControl or VkRControl => "Ctrl",
        VkMenu or VkLMenu or VkRMenu => "Alt",
        VkShift or VkLShift or VkRShift => "Shift",
        VkLWin or VkRWin => "Win",
        _ => null,
    };

    public static List<HotkeyKeyOption> CreateOptions()
    {
        var options = new List<HotkeyKeyOption>();
        var byKey = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byCode = new HashSet<int>();

        void Add(string label, string key, int code, string? note = null)
        {
            if (string.IsNullOrWhiteSpace(key) || !byKey.Add(key))
            {
                return;
            }

            options.Add(new HotkeyKeyOption(label, key, code, note));
            byCode.Add(code);
        }

        for (var i = 1; i <= 24; i++)
        {
            Add($"F{i}", $"F{i}", 0x6F + i);
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            Add(c.ToString(), c.ToString(), c);
        }

        for (var c = '0'; c <= '9'; c++)
        {
            Add(c.ToString(), c.ToString(), c);
        }

        Add("Esc", "Esc", 0x1B);
        Add("Tab", "Tab", 0x09);
        Add("Caps", "CapsLock", 0x14);
        Add("Enter", "Enter", 0x0D);
        Add("Space", "Space", 0x20);
        Add("Backspace", "Backspace", 0x08);
        Add("Insert", "Insert", 0x2D);
        Add("Delete", "Delete", 0x2E);
        Add("Home", "Home", 0x24);
        Add("End", "End", 0x23);
        Add("PageUp", "PageUp", 0x21);
        Add("PageDown", "PageDown", 0x22);
        Add("Menu", "Menu", 0x5D);
        Add("←", "Left", 0x25);
        Add("→", "Right", 0x27);
        Add("↑", "Up", 0x26);
        Add("↓", "Down", 0x28);

        Add("-", "-", 0xBD);
        Add("=", "=", 0xBB);
        Add("[", "[", 0xDB);
        Add("]", "]", 0xDD);
        Add("\\", "\\", 0xDC);
        Add(";", ";", 0xBA);
        Add("'", "'", 0xDE);
        Add(",", ",", 0xBC);
        Add(".", ".", 0xBE);
        Add("/", "/", 0xBF);
        Add("`", "`", 0xC0);

        for (var i = 0; i <= 9; i++)
        {
            Add($"Num{i}", $"Num{i}", 0x60 + i);
        }

        Add("Num *", "Multiply", 0x6A);
        Add("Num +", "Add", 0x6B);
        Add("Num -", "Subtract", 0x6D);
        Add("Num .", "Decimal", 0x6E);
        Add("Num /", "Divide", 0x6F);

        LoadAdditionalKeys(options, byKey, byCode);
        return options;
    }


    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static void LoadAdditionalKeys(
        List<HotkeyKeyOption> options,
        HashSet<string> byKey,
        HashSet<int> byCode)
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Keyboardlist.csv");
            if (!File.Exists(path))
            {
                return;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                var item = KeyboardItem.FromCsv(line);
                if (item is null
                    || item.Code <= 6
                    || string.IsNullOrWhiteSpace(item.Name)
                    || ModifierNames.Contains(item.Name.Trim())
                    || byCode.Contains(item.Code)
                    || !byKey.Add(item.Name.Trim()))
                {
                    continue;
                }

                var note = string.IsNullOrWhiteSpace(item.Note) || item.Note == "键," ? null : item.Note;
                options.Add(new HotkeyKeyOption(item.Name.Trim(), item.Name.Trim(), item.Code, note));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
