using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels.Map;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Map;

/// <summary>一跳覆盖工具视图（由星图页用 ToolWindow 承载；打开时把选中星系设为中心）。</summary>
public partial class OneJumpCoverView : UserControl
{
    public OneJumpCoverView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public OneJumpCoverViewModel ViewModel { get; } = new();

    /// <summary>设置中心星系（星图页选中项）。</summary>
    public void SetCenter(MapSystemNode? node) => ViewModel.SetCenter(node);

    private async void Compute_Click(object sender, RoutedEventArgs e) => await ViewModel.ComputeAsync();

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.Clear();
}
