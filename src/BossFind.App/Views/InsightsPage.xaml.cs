using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class InsightsPage : Page
{
    private readonly InsightsViewModel viewModel;

    public InsightsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<InsightsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        await viewModel.LoadAsync();
        if (viewModel.Summary.Length == 0)
        {
            await viewModel.AnalyzeAsync();
        }
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs args) => await viewModel.AnalyzeAsync();
}
