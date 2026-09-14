using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 可复用 KB 列表（对齐 WinUI 的 <c>KBListControl</c>，但用 <c>ui:DataGrid</c> 替代 Syncfusion）。
/// 只负责展示与交互，不持有数据源；行双击、实体名点击、右键浏览器查看都通过事件交给宿主页面处理。
/// </summary>
public partial class KillListControl : UserControl
{
    public KillListControl()
    {
        InitializeComponent();
    }

    /// <summary>列表数据源（<see cref="KBItemInfo"/> 集合）。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(KillListControl), new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>双击某一行（打开 KB 详情）。</summary>
    public event Action<KBItemInfo>? OpenKillmail;

    /// <summary>点击舰船 / 类别 / 星系 / 星域 / 受害者 / 最后一击（打开实体统计）。</summary>
    public event Action<IdName>? EntityClicked;

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: KBItemInfo info })
        {
            OpenKillmail?.Invoke(info);
        }
    }

    private void OnEntityClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: KBItemInfo info } element)
        {
            return;
        }

        var idName = (element.Tag as string) switch
        {
            "ship" => info.Type is null
                ? null
                : new IdName(info.Type.TypeID, info.Type.TypeName, IdName.CategoryEnum.InventoryType),
            "group" => info.Group is null
                ? null
                : new IdName(info.Group.GroupID, info.Group.GroupName, IdName.CategoryEnum.Group),
            "system" => info.SolarSystem is null
                ? null
                : new IdName(info.SolarSystem.SolarSystemID, info.SolarSystem.SolarSystemName, IdName.CategoryEnum.SolarSystem),
            "region" => info.Region is null
                ? null
                : new IdName(info.Region.RegionID, info.Region.RegionName, IdName.CategoryEnum.Region),
            "victim" => info.Victim,
            "finalblow" => info.FinalBlow,
            _ => null,
        };

        if (idName is not null)
        {
            EntityClicked?.Invoke(idName);
        }
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not KBItemInfo info)
        {
            return;
        }

        OpenUrl(ZkbMapping.BuildKillWebUrl(info.SKBDetail.KillmailId));
    }

    internal static void OpenUrl(string url)
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
}
