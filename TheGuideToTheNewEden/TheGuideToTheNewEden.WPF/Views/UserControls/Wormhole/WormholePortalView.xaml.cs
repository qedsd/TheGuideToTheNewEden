using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Models.Wormhole;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Wormhole;

/// <summary>洞口详情视图的展示模型（门户窗口专用，质量数据用完整千分位）。</summary>
public sealed class PortalDetailModel
{
    public PortalDetailModel(WormholePortal portal)
    {
        Portal = portal;
    }

    public WormholePortal Portal { get; }

    public string Name => Portal.Name;

    public string DestinationsText => WormholeFormat.DestinationCodesToText(Portal.Destination);

    public string AppearsInText => WormholeFormat.DestinationCodesToText(Portal.AppearsIn);

    public string LifetimeText => $"{Portal.Lifetime:0.#} h";

    public string MassRegenText => $"{WormholeFormat.MassToFullText(Portal.MassRegen)} / {FindString("General_Day")}";

    public string MaxMassPerJumpFullText => WormholeFormat.MassToFullText(Portal.MaxMassPerJump);

    public string TotalJumpMassFullText => WormholeFormat.MassToFullText(Portal.TotalJumpMass);

    public string MaxMassNoteText => Portal.MaxMassPerJumpNote?.Trim() ?? string.Empty;

    public string TotalMassNoteText => Portal.TotalJumpMassNote?.Trim() ?? string.Empty;

    public string RespawnText => string.IsNullOrEmpty(Portal.Respawn)
        ? FindString("WormholePage_Portal_Respawn_Wandering")
        : FindString(Portal.Respawn == "Static" ? "WormholePage_Portal_Respawn_Static" : "WormholePage_Portal_Respawn_Wandering");

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>
/// 洞口详情视图：基础信息 + 质量数据 + <b>过洞计算器</b>（选舰船或手输质量，
/// 判断单跳可过性并按当前总质量池估算剩余可过次数/恢复天数）+ Anoik/Ellatha 外链。
/// </summary>
public partial class WormholePortalView : UserControl
{
    private readonly PortalDetailModel _model;
    private readonly IReadOnlyList<ShipSearchItem>? _shipSearchItems;
    private bool _updatingSearch;

    public WormholePortalView(WormholePortal portal, IReadOnlyList<ShipMassOption>? shipOptions, IReadOnlyList<ShipSearchItem>? allShips = null)
    {
        InitializeComponent();
        _model = new PortalDetailModel(portal);
        _shipSearchItems = allShips;
        DataContext = _model;

        if (shipOptions is { Count: > 0 })
        {
            ShipCombo.ItemsSource = shipOptions;
            ShipCombo.SelectedIndex = 0;
        }

        UpdateResult();
    }

    private void OnShipSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShipCombo.SelectedItem is ShipMassOption option)
        {
            _updatingSearch = true;
            ShipSearchBox.Text = string.Empty;
            _updatingSearch = false;
            ShipSearchList.ItemsSource = null;
            ShipSearchFlyout.Hide();

            ShipMassBox.Value = Math.Round(option.Mass);
        }
    }

    /// <summary>输入即搜：有内容自动弹出 Flyout 展示匹配（取前 30 条），清空则收起。</summary>
    private void OnShipSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingSearch || _model is null || ShipSearchList is null || _shipSearchItems is not { Count: > 0 })
        {
            return;
        }

        var text = ShipSearchBox.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            ShipSearchFlyout.Hide();
            ShipSearchList.ItemsSource = null;
            return;
        }

        var matches = _shipSearchItems
            .Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();
        if (matches.Count == 0)
        {
            ShipSearchList.ItemsSource = null;
            ShipSearchFlyout.Hide();
            return;
        }

        ShipSearchList.ItemsSource = matches;
        // 宽度自适应：下限对齐搜索框宽度，上限由 XAML 的 MaxWidth 限制（长船名走省略号，不出横向滚动条）
        ShipSearchPanel.MinWidth = Math.Max(200, ShipSearchHost.ActualWidth);
        ShipSearchFlyout.Show();
    }

    /// <summary>选中具体舰船：把真实质量填进"舰船质量"（会自动触发一次重算），关闭 Flyout。</summary>
    private void OnShipSearchSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ShipSearchList.SelectedItem is not ShipSearchItem item)
        {
            return;
        }

        _updatingSearch = true;
        ShipSearchBox.Text = item.Name;
        _updatingSearch = false;
        ShipSearchList.ItemsSource = null;
        ShipSearchFlyout.Hide();

        ShipMassBox.Value = Math.Round(item.Mass);
    }

    private void OnMassValueChanged(object sender, RoutedEventArgs e) => UpdateResult();

    private void UpdateResult()
    {
        // InitializeComponent 期间给 UsedMassBox 赋 Value="0" 就会触发 TextChanged 回到这里，
        // 此时 _model 尚未赋值（NullReferenceException），构造末尾还会再调一次，直接跳过。
        if (_model is null || ShipMassBox is null || UsedMassBox is null)
        {
            return;
        }

        var shipMass = ShipMassBox.Value ?? 0;
        var usedMass = Math.Max(0, UsedMassBox.Value ?? 0);
        var portal = _model.Portal;

        if (shipMass <= 0)
        {
            JumpResultText.Text = FindString("WormholePage_Calculator_InputHint");
            JumpResultText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            PassesResultText.Text = string.Empty;
            return;
        }

        if (portal.MaxMassPerJump <= 0)
        {
            JumpResultText.Text = FindString("WormholePage_Calculator_NoData");
            JumpResultText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            PassesResultText.Text = string.Empty;
            return;
        }

        var remaining = portal.TotalJumpMass - usedMass;
        var canJump = shipMass <= portal.MaxMassPerJump;

        if (!canJump)
        {
            JumpResultText.Text = FindString("WormholePage_Calculator_TooHeavy");
            JumpResultText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            PassesResultText.Text = string.Format(
                FindString("WormholePage_Calculator_TooHeavyDetail"),
                WormholeFormat.MassToFullText((long)shipMass),
                WormholeFormat.MassToFullText(portal.MaxMassPerJump));
            return;
        }

        JumpResultText.Text = FindString("WormholePage_Calculator_CanPass");
        JumpResultText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");

        if (remaining >= shipMass)
        {
            var passes = (int)(remaining / shipMass);
            PassesResultText.Text = string.Format(
                FindString("WormholePage_Calculator_PassesLeft"),
                passes,
                WormholeFormat.MassToFullText(remaining));
        }
        else
        {
            // 总质量池不足：按每日再生质量估算恢复天数
            var deficit = shipMass - remaining;
            if (portal.MassRegen > 0)
            {
                var days = Math.Ceiling(deficit / (double)portal.MassRegen);
                PassesResultText.Text = string.Format(
                    FindString("WormholePage_Calculator_RegenDays"),
                    WormholeFormat.MassToFullText(remaining),
                    days.ToString("N0"),
                    WormholeFormat.MassToFullText(portal.MassRegen));
            }
            else
            {
                PassesResultText.Text = string.Format(
                    FindString("WormholePage_Calculator_MassDepleted"),
                    WormholeFormat.MassToFullText(remaining));
            }
        }
    }

    private void OnAnoikClick(object sender, RoutedEventArgs e)
        => OpenBrowser($"https://anoik.is/wormholes/{Uri.EscapeDataString(_model.Name)}");

    private void OnEllathaClick(object sender, RoutedEventArgs e)
        => OpenBrowser($"https://www.ellatha.com/eve/wormholeview.asp?hole={Uri.EscapeDataString(_model.Name)}");

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
