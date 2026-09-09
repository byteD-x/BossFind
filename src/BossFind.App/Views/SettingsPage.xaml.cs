using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<SettingsViewModel>();
        DataContext = viewModel;
    }

    private async void OnExportClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ExportAsync();
    }

    private async void OnClearWebViewProfileClick(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "清理 WebView2 Profile？",
            Content = "这会删除本地 WebView2 登录态和缓存，且无法恢复。",
            PrimaryButtonText = "清理",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        viewModel.ClearWebViewProfile();
    }
}
