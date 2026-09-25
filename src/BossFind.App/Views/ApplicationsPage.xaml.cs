using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class ApplicationsPage : Page
{
    private readonly ApplicationsViewModel viewModel;
    private bool isCompactLayout;

    public ApplicationsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<ApplicationsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateResponsiveLayout();
        ApplicationsLoadingState.Visibility = Visibility.Visible;
        ApplicationsList.Visibility = Visibility.Collapsed;
        ApplicationsEmptyState.Visibility = Visibility.Collapsed;
        await viewModel.LoadAsync();
        UpdateEmptyState();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout();

    private void UpdateEmptyState()
    {
        ApplicationsLoadingState.Visibility = viewModel.IsLoading
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplicationsList.Visibility = viewModel.IsLoading
            ? Visibility.Collapsed
            : Visibility.Visible;
        ApplicationsEmptyState.Visibility = viewModel.Applications.Count == 0 && !viewModel.IsLoading
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnBoardSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count > 0 && args.AddedItems[0] is ApplicationRow row)
        {
            viewModel.SelectedApplication = row;
        }
    }

    private void OnApplicationRowActionClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ApplicationRow row })
        {
            viewModel.SelectedApplication = row;
            ApplicationsList.SelectedItem = row;
            StageComboBox.Focus(FocusState.Programmatic);
        }
    }

    private void SelectApplication(ApplicationRow row)
    {
        viewModel.SelectedApplication = row;
        ApplicationsList.SelectedItem = row;
    }

    private async void OnContextMarkSubmittedClick(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: ApplicationRow row })
        {
            SelectApplication(row);
            if (await viewModel.MarkSubmittedAsync())
            {
                App.GlobalToasts.ShowSuccess("已标记为已投递");
            }
            UpdateEmptyState();
        }
    }

    private void OnContextUpdateStageClick(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: ApplicationRow row })
        {
            SelectApplication(row);
            StageComboBox.Focus(FocusState.Programmatic);
        }
    }

    private async void OnContextWithdrawClick(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuFlyoutItem { Tag: ApplicationRow row })
        {
            return;
        }

        SelectApplication(row);
        if (await viewModel.WithdrawApplicationAsync())
        {
            App.GlobalToasts.ShowInfo("投递记录已撤销");
        }
        UpdateEmptyState();
    }

    private void OnTaskLaneSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count > 0 && args.AddedItems[0] is ReviewTaskRow row)
        {
            viewModel.SelectedTask = row;
        }
    }

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 900;
        if (compact == isCompactLayout && ApplicationsColumns.RowDefinitions.Count > 0)
        {
            return;
        }

        isCompactLayout = compact;
        ApplicationsColumns.ColumnDefinitions.Clear();
        ApplicationsColumns.RowDefinitions.Clear();
        if (compact)
        {
            ApplicationsColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ApplicationsColumns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ApplicationsColumns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(ApplicationsPanel, 0);
            Grid.SetRow(ApplicationsPanel, 0);
            Grid.SetColumn(TasksPanel, 0);
            Grid.SetRow(TasksPanel, 1);
        }
        else
        {
            ApplicationsColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ApplicationsColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ApplicationsColumns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(ApplicationsPanel, 0);
            Grid.SetRow(ApplicationsPanel, 0);
            Grid.SetColumn(TasksPanel, 1);
            Grid.SetRow(TasksPanel, 0);
        }
    }

    private void OnOpenApplicationClick(object sender, RoutedEventArgs args)
    {
        var url = viewModel.SelectedApplication?.Application.JobPosting.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }

    private async void OnMarkSubmittedClick(object sender, RoutedEventArgs args)
    {
        var updated = await viewModel.MarkSubmittedAsync();
        UpdateEmptyState();
        if (updated)
        {
            App.GlobalToasts.ShowSuccess("已标记为已投递");
        }
    }

    private async void OnUpdateStageClick(object sender, RoutedEventArgs args)
    {
        var updated = await viewModel.UpdateApplicationStageAsync();
        UpdateEmptyState();
        if (updated)
        {
            App.GlobalToasts.ShowSuccess(viewModel.StatusMessage);
        }
    }

    private async void OnWithdrawClick(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "撤销这条投递记录？",
            Content = "记录会保留在历史中，状态改为已撤销。",
            PrimaryButtonText = "撤销记录",
            CloseButtonText = "返回",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var updated = await viewModel.WithdrawApplicationAsync();
            UpdateEmptyState();
            if (updated)
            {
                App.GlobalToasts.ShowInfo("投递记录已撤销");
            }
        }
    }

    private async void OnBeginReviewClick(object sender, RoutedEventArgs args)
    {
        var url = viewModel.SelectedTask?.Task.JobPosting.Url;
        var started = await viewModel.BeginReviewAsync();
        if (started)
        {
            App.GlobalToasts.ShowInfo("已开始复核");
        }
        if (started && !string.IsNullOrWhiteSpace(url))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }

    private async void OnCompleteReviewClick(object sender, RoutedEventArgs args)
    {
        if (await viewModel.CompleteReviewAsync())
        {
            App.GlobalToasts.ShowSuccess("复核已完成");
        }
    }

    private async void OnCancelTaskClick(object sender, RoutedEventArgs args)
    {
        if (await viewModel.CancelTaskAsync())
        {
            App.GlobalToasts.ShowInfo("复核任务已取消");
        }
    }
}
