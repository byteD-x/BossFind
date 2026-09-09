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
}
