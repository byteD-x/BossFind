using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BossFind.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel viewModel;
    private bool hasLoaded;
    private bool metricsFolded;

    public DashboardPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateResponsiveLayout();
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        await viewModel.LoadAsync();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout();

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border border
            && Microsoft.UI.Xaml.Application.Current.Resources["AppAccentBrush"] is Brush accentBrush)
        {
            border.BorderBrush = accentBrush;
        }
    }

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border border
            && Microsoft.UI.Xaml.Application.Current.Resources["AppBorderBrush"] is Brush borderBrush)
        {
            border.BorderBrush = borderBrush;
        }
    }

    private void UpdateResponsiveLayout()
    {
        var folded = ActualWidth < 1100;
        if (folded == metricsFolded && MetricsGrid.ColumnDefinitions.Count > 0)
        {
            return;
        }

        metricsFolded = folded;
        MetricsGrid.ColumnDefinitions.Clear();
        MetricsGrid.RowDefinitions.Clear();
        if (folded)
        {
            for (var column = 0; column < 4; column++)
            {
                MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            MetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var index = 0; index < MetricsGrid.Children.Count; index++)
            {
                if (MetricsGrid.Children[index] is FrameworkElement child)
                {
                    Grid.SetColumn(child, index % 4);
                    Grid.SetRow(child, index / 4);
                }
            }
        }
        else
        {
            for (var column = 0; column < 8; column++)
            {
                MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            MetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var index = 0; index < MetricsGrid.Children.Count; index++)
            {
                if (MetricsGrid.Children[index] is FrameworkElement child)
                {
                    Grid.SetRow(child, 0);
                    Grid.SetColumn(child, index);
                }
            }
        }
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("browser");
    private void OnOpenProfilesClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("profiles");
    private void OnOpenApplicationsClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("applications");
    private void OnOpenJobsClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("jobs");

    private void OnWorkflowActionClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string route } && !string.IsNullOrWhiteSpace(route))
        {
            var mainWindow = ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow;
            if (route == "browser"
                && Uri.TryCreate(viewModel.NextStepUrl, UriKind.Absolute, out _))
            {
                mainWindow?.OpenPosting(viewModel.NextStepUrl);
                return;
            }

            mainWindow?.Navigate(route);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs args) => await viewModel.LoadAsync();

    private void OnOpenPostingClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }
}
