using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace BossFind.App.Views;

public sealed partial class OnlineResumePage : Page
{
    private readonly OnlineResumeViewModel viewModel;
    private bool hasLoaded;

    public OnlineResumePage()
    {
        InitializeComponent();
        viewModel = new OnlineResumeViewModel(
            App.Services.GetRequiredService<BossFind.Application.Profiles.CandidateProfileService>());
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

    private void OnExtractClick(object sender, RoutedEventArgs args)
    {
        viewModel.Extract();
    }

    private void OnCopyItemClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: ResumeDisplayItem item })
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(item.Content);
        Clipboard.SetContent(dataPackage);
        App.GlobalToasts.ShowSuccess($"已复制：{item.Title}");
    }
}
