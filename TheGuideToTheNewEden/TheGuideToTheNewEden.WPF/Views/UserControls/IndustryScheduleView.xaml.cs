using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>工业排产：排程甘特 / 步骤物料 / 工位 / 关系图 + 候选角色改派。</summary>
public partial class IndustryScheduleView : UserControl
{
    public IndustryScheduleView()
    {
        InitializeComponent();
    }

    private IndustryPlanPageViewModel? ViewModel => DataContext as IndustryPlanPageViewModel;

    private void OnViewBomCostClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryScheduleStepViewModel step)
        {
            ViewModel?.FocusBomCost(step.Job.Model.TypeId);
        }
    }

    private void OnScheduleTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl tabs)
        {
            return;
        }

        var showTaskDetail = tabs.SelectedIndex != 4;
        if (TaskDetailColumn is not null && TaskDetailPanel is not null)
        {
            TaskDetailColumn.Width = showTaskDetail ? new GridLength(330) : new GridLength(0);
            TaskDetailPanel.Visibility = showTaskDetail ? Visibility.Visible : Visibility.Collapsed;
        }

        if (tabs.SelectedIndex == 3)
        {
            Dispatcher.BeginInvoke(new Action(() => RelationScroll.ScrollToRightEnd()), DispatcherPriority.Loaded);
        }
    }

    private ScrollViewer? _panScroll;
    private Point _panStart;
    private double _panHorizontalOffset;
    private double _panVerticalOffset;

    private void OnPanStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scroll || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindAncestor<Button>(source) is not null
            || FindAncestor<ScrollBar>(source) is not null
            || FindAncestor<Thumb>(source) is not null)
        {
            return;
        }

        _panScroll = scroll;
        _panStart = e.GetPosition(scroll);
        _panHorizontalOffset = scroll.HorizontalOffset;
        _panVerticalOffset = scroll.VerticalOffset;
        scroll.CaptureMouse();
        e.Handled = true;
    }

    private void OnPanMove(object sender, MouseEventArgs e)
    {
        if (_panScroll is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(_panScroll);
        _panScroll.ScrollToHorizontalOffset(_panHorizontalOffset - (position.X - _panStart.X));
        _panScroll.ScrollToVerticalOffset(_panVerticalOffset - (position.Y - _panStart.Y));
        e.Handled = true;
    }

    private void OnPanEnd(object sender, MouseButtonEventArgs e)
    {
        if (_panScroll is null)
        {
            return;
        }

        _panScroll.ReleaseMouseCapture();
        _panScroll = null;
        e.Handled = true;
    }

    private void OnPanLostCapture(object sender, MouseEventArgs e) => _panScroll = null;

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OnJobClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryScheduleJobViewModel job)
        {
            ViewModel?.Schedule?.SelectJob(job);
        }
    }

    private void OnLineClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryScheduleLineViewModel line)
        {
            var job = line.FirstJob;
            if (job is not null)
            {
                ViewModel?.Schedule?.SelectJob(job);
            }
        }
    }

    private void OnGraphNodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is IndustryScheduleGraphNodeViewModel node)
        {
            ViewModel?.Schedule?.SelectJob(node.Job);
        }
    }

    private void OnReassignClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not IndustryScheduleCandidateViewModel candidate)
        {
            return;
        }

        var viewModel = ViewModel;
        if (viewModel?.Schedule?.SelectedJob is null)
        {
            return;
        }
        if (!viewModel.CanReassignSchedule)
        {
            PageNotifyService.Info("载入的旧结果不能改派；请先在计划设置中重新计算。");
            return;
        }

        viewModel.ReassignScheduleJob(viewModel.Schedule.SelectedJob.Key, candidate.CharacterId);
    }

    private void OnAutoAssignClick(object sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel?.Schedule?.SelectedJob is null)
        {
            return;
        }
        if (!viewModel.CanReassignSchedule)
        {
            PageNotifyService.Info("载入的旧结果不能改派；请先在计划设置中重新计算。");
            return;
        }

        viewModel.ClearSchedulePin(viewModel.Schedule.SelectedJob.Key);
    }
}
