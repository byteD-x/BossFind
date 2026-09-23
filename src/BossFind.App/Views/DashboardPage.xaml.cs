using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel viewModel;
    private bool hasLoaded;

    public DashboardPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        await viewModel.LoadAsync();
    }

    private void OnOpenBrowserClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("browser");
    private void OnOpenProfilesClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("profiles");
    private void OnOpenApplicationsClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("applications");
    private void OnOpenJobsClick(object sender, RoutedEventArgs args) => ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("jobs");

    private async void OnRefreshClick(object sender, RoutedEventArgs args) => await viewModel.LoadAsync();

    private void OnOpenPostingClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }
}
