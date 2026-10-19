using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Tools;
using TheGuideToTheNewEden.WPF.Views.UserControls;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 快捷键选择弹窗：左侧全部按键列表，右侧模拟键盘，支持直接录制按下的组合键。
/// </summary>
public partial class HotkeyPickerWindow : FluentWindow
{
    private readonly List<HotkeyKeyOption> _options;
    private readonly Dictionary<string, HotkeyKeyOption> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HotkeyKeyOption> _byCode = [];
    private readonly Dictionary<string, List<ToggleButton>> _modifierButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RadioButton>> _keyButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _keyboardKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _modifiers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _optionsView;
    private readonly string _keyGroupName = "HotkeyPickerMainKey_" + Guid.NewGuid().ToString("N");

    private string? _mainKey;
    private bool _updating;
    private bool _capturing;

    public HotkeyPickerWindow()
    {
        InitializeComponent();

        _options = HotkeyKeyCatalog.CreateOptions();
        foreach (var option in _options)
        {
            _byKey.TryAdd(option.Key, option);
            _byCode.TryAdd(option.Code, option);
        }

        BuildKeyboard();
        BuildExtraKeys();

        _optionsView = CollectionViewSource.GetDefaultView(_options);
        _optionsView.Filter = FilterOption;
        KeyList.ItemsSource = _optionsView;

        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        Closed += OnClosed;
    }

    /// <summary>打开弹窗时带入的当前快捷键。</summary>
    public string Hotkey { get; set; } = string.Empty;

    /// <summary>点"确定/清除"后的结果；取消时保持空。</summary>
    public string SelectedHotkey { get; private set; } = string.Empty;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFromText(Hotkey);
        CaptureStatus.Text = string.Empty;
    }

    // ---------- 初始化 / 同步 ----------

    private void BuildKeyboard()
    {
        // 完整键盘：F 行和数字行左对齐（Esc 在 ` 上方，F1 在 1 上方）
        var keyboard = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        keyboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        keyboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var function = CreateRow();
        AddKey(function, "Esc", 30, "Esc");
        for (var i = 1; i <= 12; i++)
        {
            AddKey(function, $"F{i}", 30, $"F{i}");
        }

        function.HorizontalAlignment = HorizontalAlignment.Left;
        function.Margin = new Thickness(0, 0, 0, 4);
        Grid.SetRow(function, 0);
        Grid.SetColumnSpan(function, 5);
        keyboard.Children.Add(function);

        // PrtSc/Scroll/Pause 放在导航区上方（全键盘的右上角）
        var topRight = CreateRow();
        AddKey(topRight, "PrtSc", 40, "PrtSc");
        AddKey(topRight, "Scroll", 40, "SCROLL");
        AddKey(topRight, "Pause", 40, "Pause");
        topRight.HorizontalAlignment = HorizontalAlignment.Left;
        topRight.Margin = new Thickness(0, 0, 0, 4);
        Grid.SetRow(topRight, 0);
        Grid.SetColumn(topRight, 2);
        Grid.SetColumnSpan(topRight, 3);
        keyboard.Children.Add(topRight);

        var main = new StackPanel { Orientation = Orientation.Vertical };

        var number = CreateRow();
        AddKey(number, "`", 30, "`");
        for (var i = 1; i <= 9; i++)
        {
            AddKey(number, i.ToString(), 30, i.ToString());
        }

        AddKey(number, "0", 30, "0");
        AddKey(number, "-", 30, "-");
        AddKey(number, "=", 30, "=");
        AddKey(number, "Backspace", 60, "Backspace");
        main.Children.Add(number);

        var qwerty = CreateRow();
        AddKey(qwerty, "Tab", 46, "Tab");
        foreach (var c in "QWERTYUIOP")
        {
            AddKey(qwerty, c.ToString(), 32, c.ToString());
        }

        AddKey(qwerty, "[", 30, "[");
        AddKey(qwerty, "]", 30, "]");
        AddKey(qwerty, "\\", 40, "\\");
        main.Children.Add(qwerty);

        var asdf = CreateRow();
        AddKey(asdf, "Caps", 50, "CapsLock");
        foreach (var c in "ASDFGHJKL")
        {
            AddKey(asdf, c.ToString(), 32, c.ToString());
        }

        AddKey(asdf, ";", 30, ";");
        AddKey(asdf, "'", 30, "'");
        AddKey(asdf, "Enter", 58, "Enter");
        main.Children.Add(asdf);

        var zxcv = CreateRow();
        AddModifier(zxcv, "Shift", 58, "Shift");
        foreach (var c in "ZXCVBNM")
        {
            AddKey(zxcv, c.ToString(), 32, c.ToString());
        }

        AddKey(zxcv, ",", 30, ",");
        AddKey(zxcv, ".", 30, ".");
        AddKey(zxcv, "/", 30, "/");
        AddModifier(zxcv, "Shift", 58, "Shift");
        main.Children.Add(zxcv);

        var bottom = CreateRow();
        AddModifier(bottom, "Ctrl", 48, "Ctrl");
        AddModifier(bottom, "Win", 40, "Win");
        AddModifier(bottom, "Alt", 40, "Alt");
        AddKey(bottom, "Space", 180, "Space");
        AddModifier(bottom, "Alt", 40, "Alt");
        AddModifier(bottom, "Win", 40, "Win");
        AddKey(bottom, "Menu", 40, "Menu");
        AddModifier(bottom, "Ctrl", 48, "Ctrl");
        main.Children.Add(bottom);

        Grid.SetRow(main, 1);
        Grid.SetColumn(main, 0);
        keyboard.Children.Add(main);

        var nav = BuildNavCluster();
        Grid.SetRow(nav, 1);
        Grid.SetColumn(nav, 2);
        keyboard.Children.Add(nav);

        var numpad = BuildNumpadCluster();
        Grid.SetRow(numpad, 1);
        Grid.SetColumn(numpad, 4);
        keyboard.Children.Add(numpad);

        KeyboardRows.Children.Add(keyboard);
    }

    private static StackPanel CreateRow() => new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private Grid BuildNavCluster()
    {
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Top };
        for (var i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        }

        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        }

        AddGridKey(grid, "Insert", "Insert", 0, 0, width: 46);
        AddGridKey(grid, "Home", "Home", 0, 1, width: 46);
        AddGridKey(grid, "PgUp", "PageUp", 0, 2, width: 46);
        AddGridKey(grid, "Delete", "Delete", 1, 0, width: 46);
        AddGridKey(grid, "End", "End", 1, 1, width: 46);
        AddGridKey(grid, "PgDn", "PageDown", 1, 2, width: 46);
        AddGridKey(grid, "↑", "Up", 2, 1, width: 46);
        AddGridKey(grid, "←", "Left", 3, 0, width: 46);
        AddGridKey(grid, "↓", "Down", 3, 1, width: 46);
        AddGridKey(grid, "→", "Right", 3, 2, width: 46);
        return grid;
    }

    private Grid BuildNumpadCluster()
    {
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Top };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        for (var i = 0; i < 5; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        }

        AddGridKey(grid, "NumLock", "NUMLOCK", 0, 0, width: 54);
        AddGridKey(grid, "/", "Divide", 0, 1);
        AddGridKey(grid, "*", "Multiply", 0, 2);
        AddGridKey(grid, "-", "Subtract", 0, 3);

        AddGridKey(grid, "7", "Num7", 1, 0);
        AddGridKey(grid, "8", "Num8", 1, 1);
        AddGridKey(grid, "9", "Num9", 1, 2);
        AddGridKey(grid, "+", "Add", 1, 3, rowSpan: 2, width: 46, stretch: true);

        AddGridKey(grid, "4", "Num4", 2, 0);
        AddGridKey(grid, "5", "Num5", 2, 1);
        AddGridKey(grid, "6", "Num6", 2, 2);

        AddGridKey(grid, "1", "Num1", 3, 0);
        AddGridKey(grid, "2", "Num2", 3, 1);
        AddGridKey(grid, "3", "Num3", 3, 2);
        AddGridKey(grid, "Enter", "Enter", 3, 3, rowSpan: 2, width: 46, stretch: true);

        AddGridKey(grid, "0", "Num0", 4, 0, colSpan: 2, stretch: true);
        AddGridKey(grid, ".", "Decimal", 4, 2);
        return grid;
    }

    private RadioButton AddKey(Panel panel, string label, double width, string key, bool stretch = false)
    {
        var button = new RadioButton
        {
            Content = label,
            Tag = key,
            GroupName = _keyGroupName,
            Style = (Style)Resources["KeyboardKey"],
            MinWidth = width,
            Width = stretch ? double.NaN : width,
            Height = stretch ? double.NaN : 30,
            Margin = new Thickness(2),
            VerticalAlignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Center,
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
        };
        button.Checked += OnKeyChecked;
        RegisterKeyButton(key, button);
        panel.Children.Add(button);
        _keyboardKeys.Add(key);
        return button;
    }

    private void AddGridKey(
        Grid grid,
        string label,
        string key,
        int row,
        int column,
        int rowSpan = 1,
        int colSpan = 1,
        double width = 36,
        bool stretch = false)
    {
        var button = AddKey(grid, label, width, key, stretch);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column);
        Grid.SetRowSpan(button, rowSpan);
        Grid.SetColumnSpan(button, colSpan);
    }

    private void AddModifier(Panel panel, string label, double width, string key)
    {
        var button = new ToggleButton
        {
            Content = label,
            Tag = key,
            Width = width,
            Margin = new Thickness(2),
            Style = (Style)Resources["KeyboardModifier"],
        };
        button.Checked += OnModifierChanged;
        button.Unchecked += OnModifierChanged;

        if (!_modifierButtons.TryGetValue(key, out var list))
        {
            list = [];
            _modifierButtons[key] = list;
        }

        list.Add(button);
        panel.Children.Add(button);
    }

    private void RegisterKeyButton(string key, RadioButton button)
    {
        if (!_keyButtons.TryGetValue(key, out var list))
        {
            list = [];
            _keyButtons[key] = list;
        }

        list.Add(button);
    }

    /// <summary>全键盘上仍画不出来的键（F13-F24、媒体/浏览器/OEM/冷门键）放在键盘上方。</summary>
    private void BuildExtraKeys()
    {
        var missing = _options.Where(option => !_keyboardKeys.Contains(option.Key));
        AddExtraKeys(OtherPanel, missing);
    }

    private void AddExtraKeys(WrapPanel panel, IEnumerable<HotkeyKeyOption> options)
    {
        foreach (var option in options)
        {
            if (_keyButtons.ContainsKey(option.Key))
            {
                continue;
            }

            var text = new System.Windows.Controls.TextBlock
            {
                Text = option.Label,
                MaxWidth = 104,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var button = new RadioButton
            {
                Content = text,
                Tag = option.Key,
                GroupName = _keyGroupName,
                Style = (Style)Resources["KeyboardKey"],
                MinWidth = 36,
                MaxWidth = 120,
                Height = 26,
                Padding = new Thickness(4, 0, 4, 0),
                FontSize = 10,
                ToolTip = option.Display,
            };
            button.Checked += OnKeyChecked;
            RegisterKeyButton(option.Key, button);
            panel.Children.Add(button);
        }
    }

    private void LoadFromText(string? text)
    {
        _modifiers.Clear();
        _mainKey = null;

        if (!string.IsNullOrWhiteSpace(text)
            && GlobalHotkeyService.TryParse(text, out var modifiers, out var virtualKey))
        {
            if ((modifiers & 0x0002) != 0) _modifiers.Add("Ctrl");
            if ((modifiers & 0x0001) != 0) _modifiers.Add("Alt");
            if ((modifiers & 0x0004) != 0) _modifiers.Add("Shift");
            if ((modifiers & 0x0008) != 0) _modifiers.Add("Win");

            if (_byCode.TryGetValue(virtualKey, out var option))
            {
                _mainKey = option.Key;
            }
        }

        SyncModifierButtons();
        SyncKeyButtons();
        SyncListSelection();
        UpdatePreview();
    }

    private void SyncModifierButtons()
    {
        _updating = true;
        try
        {
            foreach (var pair in _modifierButtons)
            {
                var isChecked = _modifiers.Contains(pair.Key);
                foreach (var button in pair.Value)
                {
                    button.IsChecked = isChecked;
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void SyncKeyButtons()
    {
        _updating = true;
        try
        {
            foreach (var pair in _keyButtons)
            {
                var isChecked = string.Equals(pair.Key, _mainKey, StringComparison.OrdinalIgnoreCase);
                foreach (var button in pair.Value)
                {
                    button.IsChecked = isChecked;
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void SyncListSelection()
    {
        _updating = true;
        try
        {
            var option = _mainKey is not null && _byKey.TryGetValue(_mainKey, out var found) ? found : null;
            if (option is not null && _optionsView.Contains(option))
            {
                KeyList.SelectedItem = option;
                KeyList.ScrollIntoView(option);
            }
            else
            {
                KeyList.SelectedItem = null;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdatePreview()
    {
        var labels = ModifierLabels();
        if (!string.IsNullOrEmpty(_mainKey) && _byKey.TryGetValue(_mainKey, out var option))
        {
            labels.Add(option.Label);
        }

        PreviewText.Text = labels.Count == 0 ? string.Empty : string.Join(" + ", labels);
        OkButton.IsEnabled = !_capturing
            && !string.IsNullOrEmpty(_mainKey)
            && _byKey.ContainsKey(_mainKey);
    }

    private List<string> ModifierLabels()
    {
        var labels = new List<string>();
        foreach (var modifier in HotkeyKeyCatalog.ModifierOrder)
        {
            if (_modifiers.Contains(modifier))
            {
                labels.Add(modifier);
            }
        }

        return labels;
    }

    private string BuildHotkey()
    {
        if (string.IsNullOrEmpty(_mainKey) || !_byKey.TryGetValue(_mainKey, out var option))
        {
            return string.Empty;
        }

        var parts = ModifierLabels();
        parts.Add(option.Key);
        return string.Join("+", parts);
    }

    // ---------- 选择事件 ----------

    private void OnKeySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || KeyList.SelectedItem is not HotkeyKeyOption option)
        {
            return;
        }

        _mainKey = option.Key;
        SyncKeyButtons();
        UpdatePreview();
    }

    private void OnKeyChecked(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not RadioButton { Tag: string key })
        {
            return;
        }

        _mainKey = key;
        SyncListSelection();
        UpdatePreview();
    }

    private void OnModifierChanged(object sender, RoutedEventArgs e)
    {
        if (_updating || sender is not ToggleButton { Tag: string key } button)
        {
            return;
        }

        if (button.IsChecked == true)
        {
            _modifiers.Add(key);
        }
        else
        {
            _modifiers.Remove(key);
        }

        SyncModifierButtons();
        UpdatePreview();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_optionsView is null)
        {
            return;
        }

        _optionsView.Refresh();
        SyncListSelection();
    }

    private bool FilterOption(object item)
    {
        if (item is not HotkeyKeyOption option)
        {
            return false;
        }

        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            return true;
        }

        return option.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || option.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (option.Note?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    // ---------- 录制 ----------

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
            CaptureStatus.Text = FindString("HotkeyPicker_CaptureStopped");
            return;
        }

        StartCapture();
    }

    private void StartCapture()
    {
        _capturing = true;
        _modifiers.Clear();
        _mainKey = null;
        SyncModifierButtons();
        SyncKeyButtons();
        SyncListSelection();
        UpdatePreview();

        CaptureButton.Content = FindString("HotkeyPicker_StopCapture");
        CaptureStatus.Text = FindString("HotkeyPicker_Capturing");
        SetEditingEnabled(false);

        KeyboardHookService.Current.KeyPressed += OnCaptureKeyPressed;
        KeyboardHookService.Current.Start();

        if (!KeyboardHookService.Current.IsRunning)
        {
            StopCapture();
            CaptureStatus.Text = FindString("HotkeyPicker_CaptureFailed");
            return;
        }

        // 录制时吞掉系统按键：Win/Alt+Tab/Esc 不会打开开始菜单、切窗口或关弹窗。
        KeyboardHookService.Current.SuppressAll = true;
        CaptureSink.Focus();
    }

    private void OnCaptureKeyPressed(KeyboardKeyInfo key)
    {
        if (!_capturing)
        {
            return;
        }

        // 修饰键只改变按住状态，等主键按下再一起提交
        if (HotkeyKeyCatalog.GetModifierName(key.VirtualKey) is not null)
        {
            return;
        }

        // Esc 用来取消录制；如果就是要 Esc，请从左侧列表/键盘选择
        if (key.VirtualKey == 0x1B)
        {
            StopCapture();
            CaptureStatus.Text = FindString("HotkeyPicker_CaptureCanceled");
            return;
        }

        if (!_byCode.TryGetValue(key.VirtualKey, out var option))
        {
            CaptureStatus.Text = FindString("HotkeyPicker_CaptureUnsupported");
            return;
        }

        var modifiers = new HashSet<string>(
            HotkeyKeyCatalog.GetCurrentModifiers(),
            StringComparer.OrdinalIgnoreCase);

        // SuppressAll 期间 GetAsyncKeyState 可能看不到刚按下的修饰键，
        // 以低级钩子自己维护的按住集合为准（勾子已把 A 也加进去了，按 VK 取修饰键不受影响）。
        var service = KeyboardHookService.Current;
        if (service.IsPressed(0x11) || service.IsPressed(0xA2) || service.IsPressed(0xA3)) modifiers.Add("Ctrl");
        if (service.IsPressed(0x12) || service.IsPressed(0xA4) || service.IsPressed(0xA5)) modifiers.Add("Alt");
        if (service.IsPressed(0x10) || service.IsPressed(0xA0) || service.IsPressed(0xA1)) modifiers.Add("Shift");
        if (service.IsPressed(0x5B) || service.IsPressed(0x5C)) modifiers.Add("Win");

        _modifiers.Clear();
        foreach (var modifier in modifiers)
        {
            _modifiers.Add(modifier);
        }

        _mainKey = option.Key;
        SyncModifierButtons();
        SyncKeyButtons();
        SyncListSelection();
        UpdatePreview();

        StopCapture();
        CaptureStatus.Text = string.Format(FindString("HotkeyPicker_Captured"), PreviewText.Text);
    }

    private void StopCapture()
    {
        if (!_capturing)
        {
            return;
        }

        _capturing = false;
        KeyboardHookService.Current.KeyPressed -= OnCaptureKeyPressed;
        KeyboardHookService.Current.Stop();
        KeyboardHookService.Current.SuppressAll = false;

        CaptureButton.Content = FindString("HotkeyPicker_Capture");
        SetEditingEnabled(true);

        // 录制键的 key-up 可能在恢复 SuppressAll 之后才到；把焦点留在非按钮区域，避免 Enter/Space 误触按钮
        if (IsVisible)
        {
            CaptureSink.Focus();
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        SearchBox.IsEnabled = enabled;
        KeyList.IsEnabled = enabled;
        KeyboardRows.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        OkButton.IsEnabled = enabled
            && !string.IsNullOrEmpty(_mainKey)
            && _byKey.ContainsKey(_mainKey);
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
            CaptureStatus.Text = FindString("HotkeyPicker_CaptureFocusLost");
        }
    }

    // ---------- 按钮 ----------

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
        }

        var hotkey = BuildHotkey();
        if (hotkey.Length == 0)
        {
            return;
        }

        SelectedHotkey = hotkey;
        DialogResult = true;
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            StopCapture();
        }

        SelectedHotkey = string.Empty;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        StopCapture();
        DialogResult = false;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        StopCapture();
        KeyboardHookService.Current.KeyPressed -= OnCaptureKeyPressed;
        KeyboardHookService.Current.SuppressAll = false;
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
