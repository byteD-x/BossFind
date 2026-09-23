using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class ApplicationsPage : Page
{
    private readonly ApplicationsViewModel viewModel;

    public ApplicationsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<ApplicationsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args) => await viewModel.LoadAsync();

    private void OnOpenApplicationClick(object sender, RoutedEventArgs args)
    {
        var url = viewModel.SelectedApplication?.Application.JobPosting.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }

    private async void OnMarkSubmittedClick(object sender, RoutedEventArgs args) => await viewModel.MarkSubmittedAsync();

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
            await viewModel.WithdrawApplicationAsync();
        }
    }

    private async void OnBeginReviewClick(object sender, RoutedEventArgs args)
    {
        var url = viewModel.SelectedTask?.Task.JobPosting.Url;
        var started = await viewModel.BeginReviewAsync();
        if (started && !string.IsNullOrWhiteSpace(url))
        {
            ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.OpenPosting(url);
        }
    }

    private async void OnCompleteReviewClick(object sender, RoutedEventArgs args) => await viewModel.CompleteReviewAsync();

    private async void OnCancelTaskClick(object sender, RoutedEventArgs args) => await viewModel.CancelTaskAsync();
}
