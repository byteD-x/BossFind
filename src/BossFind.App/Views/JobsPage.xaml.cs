using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class JobsPage : Page
{
    private readonly JobsViewModel viewModel;

    public JobsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<JobsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args) => await viewModel.LoadAsync();

    private void OnSearchTextChanged(object sender, TextChangedEventArgs args)
    {
        viewModel.SearchText = SearchBox.Text;
    }

    private async void OnToggleFavoriteClick(object sender, RoutedEventArgs args) => await viewModel.ToggleFavoriteAsync();

    private async void OnQueueClick(object sender, RoutedEventArgs args) => await viewModel.QueueAsync();

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
