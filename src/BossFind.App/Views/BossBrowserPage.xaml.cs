using BossFind.App.ViewModels;
using BossFind.Platform.Boss.Parsing;
using BossFind.Platform.Boss.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace BossFind.App.Views;

public sealed partial class BossBrowserPage : Page
{
    private readonly BossBrowserViewModel viewModel;
    private bool hasInitialized;

    public BossBrowserPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<BossBrowserViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (hasInitialized)
        {
            return;
        }

        hasInitialized = true;
        var result = await BossWebViewInitializer.InitializeAsync(
            BossWebView,
            new BossWebViewEnvironmentOptions());
        viewModel.Status = result.Message;

        if (!result.IsSuccess || result.Environment is null)
        {
            return;
        }

        viewModel.ProfilePath = result.Environment.UserDataFolder;
        BossWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "WebView2");
        BossWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "fixture.bossfind.local",
            fixtureDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
        BossWebView.Source = new Uri("https://fixture.bossfind.local/fixture.html");
    }

    private async void OnNavigationCompleted(
        CoreWebView2 sender,
        CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            viewModel.Status = $"固定岗位页面加载失败：{args.WebErrorStatus}";
            return;
        }

        try
        {
            var serializedHtml = await sender.ExecuteScriptAsync("document.documentElement.outerHTML");
            var html = JsonSerializer.Deserialize<string>(serializedHtml) ?? string.Empty;
            var summary = BossJobSummaryParser.Parse(html);
            viewModel.SetJobSummary(summary.Title, summary.Company, summary.City, summary.Tags);
            viewModel.Status = "固定岗位页面已加载并解析。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"岗位摘要解析失败：{exception.Message}";
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ImportAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (BossWebView.CoreWebView2 is not null)
        {
            BossWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }

        BossWebView.Close();
        hasInitialized = false;
    }
}
