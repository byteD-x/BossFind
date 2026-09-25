using BossFind.App.ViewModels;
using BossFind.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class JobsPage : Page
{
    private readonly JobsViewModel viewModel;
    private bool isCompactLayout;

    public JobsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<JobsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateResponsiveLayout();
        await viewModel.LoadAsync();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 960;
        if (compact == isCompactLayout && PageLayout.RowDefinitions.Count > 0)
        {
            return;
        }

        isCompactLayout = compact;
        PageLayout.ColumnDefinitions.Clear();
        PageLayout.RowDefinitions.Clear();
        PageLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (compact)
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(DetailsPanel, 0);
            Grid.SetRow(DetailsPanel, 2);
        }
        else
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(DetailsPanel, 1);
            Grid.SetRow(DetailsPanel, 1);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args)
    {
        viewModel.SearchText = SearchBox.Text;
    }

    private async void OnToggleFavoriteClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ToggleFavoriteAsync();
        App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
    }

    private async void OnContextFavoriteClick(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: JobPosting posting })
        {
            viewModel.SelectedPosting = posting;
            JobsListView.SelectedItem = posting;
            await viewModel.ToggleFavoriteAsync();
            App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
        }
    }

    private async void OnQueueClick(object sender, RoutedEventArgs args)
    {
        await viewModel.QueueAsync();
        App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
    }

    private async void OnContextQueueClick(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem { Tag: JobPosting posting })
        {
            viewModel.SelectedPosting = posting;
            JobsListView.SelectedItem = posting;
            await viewModel.QueueAsync();
            App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
        }
    }

    private void OnOpenPostingClick(object sender, RoutedEventArgs args)
    {
        if (viewModel.SelectedPosting is { Url.Length: > 0 } posting)
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(posting.Url);
        }
        else
        {
            viewModel.StatusMessage = "这个岗位没有可打开的网页地址。";
        }
    }
}
