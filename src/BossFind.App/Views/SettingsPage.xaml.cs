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
        App.GlobalToasts.ShowInfo(viewModel.Status);
    }

    private async void OnBackupClick(object sender, RoutedEventArgs args)
    {
        await viewModel.BackupAsync();
        App.GlobalToasts.ShowInfo(viewModel.Status);
    }

    private async void OnExportApplicationsClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ExportApplicationsAsync();
        App.GlobalToasts.ShowInfo(viewModel.Status);
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            Title = "从本地备份恢复？",
            Content = "当前本地数据会被备份文件覆盖。恢复完成后请重启 BossFind。",
            PrimaryButtonText = "恢复",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await viewModel.RestoreAsync();
            App.GlobalToasts.ShowInfo(viewModel.Status);
        }
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
        App.GlobalToasts.ShowInfo(viewModel.Status);
    }
}
