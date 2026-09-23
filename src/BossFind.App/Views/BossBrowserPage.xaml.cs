using BossFind.App.ViewModels;
using BossFind.Application.Insights;
using BossFind.Application.Jobs;
using BossFind.Domain.Entities;
using BossFind.Platform.Boss.Parsing;
using BossFind.Platform.Boss.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Windows.System;

namespace BossFind.App.Views;

public sealed partial class BossBrowserPage : Page
{
    private readonly BossBrowserViewModel viewModel;
    private readonly JobPostingService jobPostingService;
    private readonly IJobInsightService insightService;
    private bool hasInitialized;
    private bool isPageActive;
    private bool? isCompactLayout;
    private string? pendingUrl;
    private JobPosting? currentPosting;
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record PageMetadata(
        string Description,
        string SiteName,
        string CanonicalUrl,
        string Keywords,
        string Author,
        string Language,
        string FaviconUrl,
        int TextLength,
        int LinksCount);

    public BossBrowserPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<BossBrowserViewModel>();
        jobPostingService = App.Services.GetRequiredService<JobPostingService>();
        insightService = App.Services.GetRequiredService<IJobInsightService>();
        DataContext = viewModel;
    }

    private void OnUrlKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            OpenUrl(UrlBox.Text.Trim());
        }
    }

    private void OnFocusUrlAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        UrlBox.Focus(FocusState.Keyboard);
        UrlBox.SelectAll();
    }

    public void OpenUrl(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            viewModel.Status = "请输入有效的 HTTP 或 HTTPS 地址。";
            return;
        }

        UrlBox.Text = url.AbsoluteUri;
        if (BossWebView.CoreWebView2 is null)
        {
            pendingUrl = url.AbsoluteUri;
            return;
        }

        BossWebView.Source = url;
    }

    private void OnFixtureClick(object sender, RoutedEventArgs args) => OpenUrl("https://fixture.bossfind.local/fixture.html");

    private void OnBackClick(object sender, RoutedEventArgs args)
    {
        if (BossWebView.CoreWebView2?.CanGoBack == true) BossWebView.CoreWebView2.GoBack();
    }

    private void OnForwardClick(object sender, RoutedEventArgs args)
    {
        if (BossWebView.CoreWebView2?.CanGoForward == true) BossWebView.CoreWebView2.GoForward();
    }

    // 加载中点击为停止，空闲时点击为刷新
    private void OnReloadStopClick(object sender, RoutedEventArgs args)
    {
        var coreWebView2 = BossWebView.CoreWebView2;
        if (coreWebView2 is null) return;
        if (viewModel.IsLoading)
        {
            coreWebView2.Stop();
        }
        else
        {
            coreWebView2.Reload();
        }
    }

    private async void OnOpenExternalClick(object sender, RoutedEventArgs args)
    {
        if (Uri.TryCreate(viewModel.CurrentUrl, UriKind.Absolute, out var url))
        {
            await Launcher.LaunchUriAsync(url);
        }
    }

    private void OnPaneToggleClick(object sender, RoutedEventArgs args)
    {
        BrowserSplit.IsPaneOpen = !BrowserSplit.IsPaneOpen;
        PaneToggleButton.IsChecked = BrowserSplit.IsPaneOpen;
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 900;
        if (isCompactLayout == compact)
        {
            PaneToggleButton.IsChecked = BrowserSplit.IsPaneOpen;
            return;
        }

        isCompactLayout = compact;
        if (compact)
        {
            BrowserSplit.DisplayMode = SplitViewDisplayMode.Overlay;
            BrowserSplit.IsPaneOpen = false;
        }
        else
        {
            BrowserSplit.DisplayMode = SplitViewDisplayMode.Inline;
            BrowserSplit.IsPaneOpen = true;
        }

        PaneToggleButton.IsChecked = BrowserSplit.IsPaneOpen;
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs args) => SetZoom(viewModel.ZoomFactor - 0.1);

    private void OnZoomInClick(object sender, RoutedEventArgs args) => SetZoom(viewModel.ZoomFactor + 0.1);

    private void OnZoomResetClick(object sender, RoutedEventArgs args) => SetZoom(1.0);

    private void SetZoom(double factor)
    {
        var normalized = Math.Clamp(Math.Round(factor, 2), 0.5, 2.0);
        viewModel.ZoomFactor = normalized;
        _ = ApplyPageZoomAsync();
    }

    private async void OnFavoriteClick(object sender, RoutedEventArgs args)
    {
        if (currentPosting is null || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var isFavorite = !currentPosting.IsFavorite;
            await jobPostingService.SetFavoriteAsync(currentPosting, isFavorite);
            currentPosting.IsFavorite = isFavorite;
            viewModel.IsFavorite = isFavorite;
            viewModel.Status = isFavorite ? "岗位已收藏。" : "岗位已取消收藏。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"收藏状态更新失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (hasInitialized) return;
        hasInitialized = true;
        isPageActive = true;
        UpdateResponsiveLayout();
        await viewModel.LoadProfilesAsync();
        if (!isPageActive || !hasInitialized) return;
        var result = await BossWebViewInitializer.InitializeAsync(BossWebView, new BossWebViewEnvironmentOptions());
        if (!isPageActive || !hasInitialized) return;
        viewModel.Status = result.Message;
        if (!result.IsSuccess || result.Environment is null) return;
        viewModel.ProfilePath = result.Environment.UserDataFolder;
        BossWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        BossWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        BossWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        BossWebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
        BossWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        BossWebView.CoreWebView2.HistoryChanged += OnHistoryChanged;
        BossWebView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
        BossWebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        BossWebView.CoreWebView2.SourceChanged += OnSourceChanged;
        BossWebView.CoreWebView2.ProcessFailed += OnProcessFailed;
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "WebView2");
        BossWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("fixture.bossfind.local", fixtureDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
        var initialUrl = pendingUrl ?? UrlBox.Text;
        pendingUrl = null;
        OpenUrl(initialUrl);
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        viewModel.IsLoading = true;
        viewModel.CurrentUrl = args.Uri;
    }

    private void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        viewModel.CurrentUrl = sender.Source;
        UrlBox.Text = sender.Source;
    }

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        viewModel.IsLoading = false;
        viewModel.Status = $"网页组件异常（{args.ProcessFailedKind}），请点击刷新重试。";
    }

    // 网页标题、地址与历史状态变化时同步到界面
    private void OnDocumentTitleChanged(CoreWebView2 sender, object args)
    {
        viewModel.PageTitle = string.IsNullOrWhiteSpace(sender.DocumentTitle) ? "未命名页面" : sender.DocumentTitle;
    }

    private void OnHistoryChanged(CoreWebView2 sender, object args)
    {
        viewModel.CanGoBack = sender.CanGoBack;
        viewModel.CanGoForward = sender.CanGoForward;
    }

    // 站内打开弹窗页面（如平台登录跳转），避免弹出独立窗口打断嵌入体验
    private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var url))
        {
            viewModel.IsLoading = true;
            BossWebView.Source = url;
        }
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!isPageActive || !hasInitialized) return;
        viewModel.IsLoading = false;
        viewModel.CurrentUrl = sender.Source;
        UrlBox.Text = sender.Source;

        if (!args.IsSuccess)
        {
            viewModel.Status = $"页面加载失败：{args.WebErrorStatus}";
            return;
        }
        try
        {
            await ApplyPageZoomAsync();
            if (!isPageActive || !hasInitialized) return;
            var serializedHtml = await sender.ExecuteScriptAsync("document.documentElement.outerHTML");
            var html = JsonSerializer.Deserialize<string>(serializedHtml) ?? string.Empty;
            await UpdatePageMetadataAsync(sender);
            if (!isPageActive || !hasInitialized) return;
            var summary = BossJobSummaryParser.Parse(html);
            viewModel.SetJobSummary(summary.Title, summary.Company, summary.City, summary.Tags, summary.Salary, summary.Experience, summary.Education, summary.BenefitList, summary.Description);
            if (string.IsNullOrWhiteSpace(summary.Title))
            {
                currentPosting = null;
                viewModel.Status = "当前页面未识别到岗位详情，请打开具体岗位页面后重试。";
                return;
            }

            currentPosting = await jobPostingService.RecordViewedAsync(
                new JobPostingDraft(summary.Title, summary.Company, summary.City, summary.Tags, summary.Salary, summary.Experience, summary.Education, summary.BenefitList, summary.Description, summary.ExternalId, summary.Url),
                sender.Source,
                CancellationToken.None);
            if (!isPageActive || !hasInitialized) return;
            viewModel.IsFavorite = currentPosting.IsFavorite;
            viewModel.Status = "岗位页面已加载，岗位信息已记录到本地历史。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"岗位摘要解析失败：{exception.Message}";
        }
    }

    private async Task ApplyPageZoomAsync()
    {
        if (!isPageActive || !hasInitialized || BossWebView.CoreWebView2 is null)
        {
            return;
        }

        var zoom = viewModel.ZoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await BossWebView.ExecuteScriptAsync($"document.documentElement.style.zoom = '{zoom}';");
    }

    private async Task UpdatePageMetadataAsync(CoreWebView2 sender)
    {
        if (!isPageActive || !hasInitialized) return;
        const string metadataScript = """
            JSON.stringify({
              description: document.querySelector('meta[name="description"]')?.content || '',
              siteName: document.querySelector('meta[property="og:site_name"]')?.content || '',
              canonicalUrl: document.querySelector('link[rel="canonical"]')?.href || '',
              keywords: document.querySelector('meta[name="keywords"]')?.content || '',
              author: document.querySelector('meta[name="author"]')?.content || '',
              language: document.documentElement.lang || '',
              faviconUrl: document.querySelector('link[rel*="icon"]')?.href || '',
              textLength: (document.body?.innerText || '').trim().length,
              linksCount: document.links.length
            })
            """;

        var serializedMetadata = await sender.ExecuteScriptAsync(metadataScript);
        if (!isPageActive || !hasInitialized) return;
        var metadataJson = JsonSerializer.Deserialize<string>(serializedMetadata) ?? "{}";
        var metadata = JsonSerializer.Deserialize<PageMetadata>(
            metadataJson,
            MetadataJsonOptions);
        if (metadata is null)
        {
            return;
        }

        viewModel.PageMetaDescription = metadata.Description;
        viewModel.PageSiteName = metadata.SiteName;
        viewModel.PageCanonicalUrl = metadata.CanonicalUrl;
        viewModel.PageKeywords = metadata.Keywords;
        viewModel.PageAuthor = metadata.Author;
        viewModel.PageLanguage = metadata.Language;
        viewModel.PageFaviconUrl = metadata.FaviconUrl;
        viewModel.PageTextLength = metadata.TextLength;
        viewModel.PageLinksCount = metadata.LinksCount;
    }

    private async void OnInsightClick(object sender, RoutedEventArgs args)
    {
        if (currentPosting is null || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var insight = await insightService.CreateAsync(currentPosting);
            var dialog = new ContentDialog
            {
                Title = $"岗位分析 · {insight.Provider}",
                Content = $"{insight.Summary}\n\n{string.Join("\n", insight.Suggestions)}",
                CloseButtonText = "关闭",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception exception)
        {
            viewModel.Status = $"岗位分析失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private async void OnPdfClick(object sender, RoutedEventArgs args)
    {
        var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BossFind", $"岗位-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
        try
        {
            var exported = await WebView2PdfExporter.ExportAsync(BossWebView.CoreWebView2, outputPath);
            viewModel.Status = exported ? $"岗位 PDF 已导出：{outputPath}" : "岗位 PDF 导出失败。";
        }
        catch (Exception exception) { viewModel.Status = $"岗位 PDF 导出失败：{exception.Message}"; }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs args)
    {
        if (currentPosting is null || viewModel.IsBusy) return;
        var dialog = new ContentDialog
        {
            Title = "打开投递页面？",
            Content = "应用只会记录待确认投递，并打开招聘平台页面。不会自动填写或提交。",
            PrimaryButtonText = "打开",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        viewModel.IsBusy = true;
        try
        {
            await jobPostingService.CreateApplicationAsync(currentPosting, viewModel.SelectedCandidateProfile?.Id, "等待用户在平台页面确认提交");
            if (Uri.TryCreate(currentPosting.Url, UriKind.Absolute, out var url)) BossWebView.Source = url;
            viewModel.Status = "已创建待确认投递记录，请在平台页面自行核对并提交。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"创建投递记录失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private async void OnQueueClick(object sender, RoutedEventArgs args)
    {
        if (currentPosting is null || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            await jobPostingService.QueueAsync(currentPosting, viewModel.SelectedCandidateProfile?.Id);
            viewModel.Status = "岗位已加入人工复核队列。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"加入复核队列失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs args) => await viewModel.ImportAsync();
    private async void OnMatchClick(object sender, RoutedEventArgs args) => await viewModel.MatchAsync();

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        isPageActive = false;
        if (BossWebView.CoreWebView2 is not null)
        {
            BossWebView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            BossWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            BossWebView.CoreWebView2.HistoryChanged -= OnHistoryChanged;
            BossWebView.CoreWebView2.DocumentTitleChanged -= OnDocumentTitleChanged;
            BossWebView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
            BossWebView.CoreWebView2.SourceChanged -= OnSourceChanged;
            BossWebView.CoreWebView2.ProcessFailed -= OnProcessFailed;
        }
        BossWebView.Close();
        hasInitialized = false;
    }
}
