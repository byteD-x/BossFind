using BossFind.App.ViewModels;
using BossFind.Application.Insights;
using BossFind.Application.Jobs;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Platform.Boss.Parsing;
using BossFind.Platform.Boss.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace BossFind.App.Views;

public sealed partial class BossBrowserPage : Page, IDisposable
{
    private readonly BossBrowserViewModel viewModel;
    private readonly JobPostingService jobPostingService;
    private readonly IJobInsightService insightService;
    private readonly IJobRepository jobRepository;
    private bool hasInitialized;
    private bool isPageActive;
    private bool? isCompactLayout;
    private CancellationTokenSource? initializationCancellation;
    private string? pendingUrl;
    private JobPosting? currentPosting;
    private JobPostingDraft? currentPostingDraft;
    private JobPostingDraft[] searchResultDrafts = [];
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

    private sealed record ResumeFillResult(
        bool Filled,
        string FieldKey,
        string Reason,
        string Field = "");

    public BossBrowserPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<BossBrowserViewModel>();
        jobPostingService = App.Services.GetRequiredService<JobPostingService>();
        insightService = App.Services.GetRequiredService<IJobInsightService>();
        jobRepository = App.Services.GetRequiredService<IJobRepository>();
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

    public void SubmitCurrentApplication()
    {
        OnApplyClick(this, new RoutedEventArgs());
    }

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

    private void OnPaneResizeThumbDragDelta(object sender, DragDeltaEventArgs args)
    {
        if (isCompactLayout == true || !BrowserSplit.IsPaneOpen)
        {
            return;
        }

        BrowserSplit.OpenPaneLength = Math.Clamp(
            BrowserSplit.OpenPaneLength - args.HorizontalChange,
            320,
            560);
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
        if (!viewModel.IsSummaryReady || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var posting = await EnsureCurrentPostingSavedAsync();
            if (posting is null)
            {
                return;
            }

            var isFavorite = !posting.IsFavorite;
            await jobPostingService.SetFavoriteAsync(posting, isFavorite);
            posting.IsFavorite = isFavorite;
            viewModel.IsFavorite = isFavorite;
            viewModel.Status = isFavorite ? "岗位已收藏。" : "岗位已取消收藏。";
            App.GlobalToasts.ShowSuccess(viewModel.Status);
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

    private async void OnSavePostingClick(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsSummaryReady || viewModel.IsBusy)
        {
            return;
        }

        viewModel.IsBusy = true;
        try
        {
            var posting = await EnsureCurrentPostingSavedAsync();
            viewModel.Status = posting is null ? "当前页面没有可记录的岗位。" : "岗位已记录，可在岗位记录中继续处理。";
            if (posting is not null)
            {
                App.GlobalToasts.ShowSuccess("岗位已记录");
            }
        }
        catch (Exception exception)
        {
            viewModel.Status = $"记录岗位失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (isPageActive)
        {
            return;
        }

        isPageActive = true;
        UpdateResponsiveLayout();
        if (hasInitialized)
        {
            AttachWebViewEvents();
            return;
        }

        initializationCancellation?.Dispose();
        initializationCancellation = new CancellationTokenSource();
        var cancellationToken = initializationCancellation.Token;
        try
        {
            await viewModel.LoadProfilesAsync(cancellationToken);
            if (!isPageActive || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (BossWebView.CoreWebView2 is null)
            {
                var result = await BossWebViewInitializer.InitializeAsync(
                    BossWebView,
                    new BossWebViewEnvironmentOptions(),
                    cancellationToken);
                if (!isPageActive || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                viewModel.Status = result.Message;
                if (!result.IsSuccess || result.Environment is null || BossWebView.CoreWebView2 is null)
                {
                    return;
                }

                viewModel.ProfilePath = result.Environment.UserDataFolder;
            }

            ConfigureWebView();
            AttachWebViewEvents();
            hasInitialized = true;
            var initialUrl = pendingUrl;
            pendingUrl = null;
            if (!string.IsNullOrWhiteSpace(initialUrl))
            {
                OpenUrl(initialUrl);
            }
            else if (BossWebView.Source is null)
            {
                OpenUrl(UrlBox.Text);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            hasInitialized = false;
        }
        catch (Exception exception)
        {
            hasInitialized = false;
            viewModel.Status = $"浏览器初始化失败：{exception.Message}";
        }
    }

    private void ConfigureWebView()
    {
        var coreWebView2 = BossWebView.CoreWebView2;
        if (coreWebView2 is null)
        {
            return;
        }

        coreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        coreWebView2.Settings.IsStatusBarEnabled = false;
        coreWebView2.Settings.IsZoomControlEnabled = false;
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "WebView2");
        coreWebView2.SetVirtualHostNameToFolderMapping(
            "fixture.bossfind.local",
            fixtureDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);
    }

    private void AttachWebViewEvents()
    {
        var coreWebView2 = BossWebView.CoreWebView2;
        if (coreWebView2 is null)
        {
            return;
        }

        coreWebView2.NavigationStarting -= OnNavigationStarting;
        coreWebView2.NavigationCompleted -= OnNavigationCompleted;
        coreWebView2.HistoryChanged -= OnHistoryChanged;
        coreWebView2.DocumentTitleChanged -= OnDocumentTitleChanged;
        coreWebView2.NewWindowRequested -= OnNewWindowRequested;
        coreWebView2.SourceChanged -= OnSourceChanged;
        coreWebView2.ProcessFailed -= OnProcessFailed;
        coreWebView2.NavigationStarting += OnNavigationStarting;
        coreWebView2.NavigationCompleted += OnNavigationCompleted;
        coreWebView2.HistoryChanged += OnHistoryChanged;
        coreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;
        coreWebView2.NewWindowRequested += OnNewWindowRequested;
        coreWebView2.SourceChanged += OnSourceChanged;
        coreWebView2.ProcessFailed += OnProcessFailed;
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        viewModel.IsLoading = true;
        viewModel.CurrentUrl = args.Uri;
        currentPosting = null;
        currentPostingDraft = null;
        searchResultDrafts = [];
        viewModel.SetSearchResults(0);
        viewModel.IsPostingSaved = false;
        viewModel.IsFavorite = false;
        viewModel.SetJobSummary(string.Empty, string.Empty, string.Empty, []);
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
                currentPostingDraft = null;
                viewModel.IsPostingSaved = false;
                viewModel.IsFavorite = false;
                var searchResults = BossJobSearchResultParser.Parse(html, sender.Source);
                searchResultDrafts = searchResults
                    .Select(result => new JobPostingDraft(
                        result.Title,
                        result.Company,
                        result.City,
                        result.SkillTags,
                        result.Salary,
                        result.Experience,
                        result.Education,
                        null,
                        string.Empty,
                        result.ExternalId,
                        result.Url))
                    .ToArray();
                viewModel.SetSearchResults(searchResultDrafts.Length);
                viewModel.Status = searchResultDrafts.Length > 0
                    ? "已识别岗位搜索结果，可批量保存。"
                    : "当前页面未识别到岗位详情或搜索结果。";
                return;
            }

            searchResultDrafts = [];
            viewModel.SetSearchResults(0);

            currentPostingDraft = new JobPostingDraft(
                summary.Title,
                summary.Company,
                summary.City,
                summary.Tags,
                summary.Salary,
                summary.Experience,
                summary.Education,
                summary.BenefitList,
                summary.Description,
                summary.ExternalId,
                summary.Url);
            currentPosting = await FindExistingPostingAsync(currentPostingDraft, sender.Source);
            viewModel.IsPostingSaved = currentPosting is not null;
            viewModel.IsFavorite = currentPosting?.IsFavorite == true;
            viewModel.Status = currentPosting is null
                ? "已识别岗位，是否记录由你决定。"
                : "已加载已记录岗位。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"岗位摘要解析失败：{exception.Message}";
        }
    }

    private async void OnSaveSearchResultsClick(object sender, RoutedEventArgs args)
    {
        if (searchResultDrafts.Length == 0 || viewModel.IsBusy)
        {
            return;
        }

        viewModel.IsBusy = true;
        try
        {
            var saved = await jobPostingService.RecordViewedBatchAsync(searchResultDrafts);
            searchResultDrafts = [];
            viewModel.SetSearchResults(0);
            viewModel.Status = $"已保存 {saved.Count} 个岗位搜索结果，重复岗位已合并。";
            App.GlobalToasts.ShowSuccess($"已保存 {saved.Count} 个岗位");
        }
        catch (OperationCanceledException)
        {
            viewModel.Status = "保存搜索结果已取消。";
        }
        catch (Exception exception)
        {
            viewModel.Status = $"保存搜索结果失败：{exception.Message}";
        }
        finally
        {
            viewModel.IsBusy = false;
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
        if (!viewModel.IsSummaryReady || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var insight = await insightService.CreateAsync(BuildCurrentPosting());
            var dialog = new ContentDialog
            {
                Title = $"岗位分析 · {insight.Provider}",
                Content = $"{insight.Summary}\n\n{string.Join("\n", insight.Suggestions)}",
                CloseButtonText = "关闭",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            App.GlobalToasts.ShowInfo("岗位分析已生成");
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
            if (exported)
            {
                App.GlobalToasts.ShowSuccess("岗位 PDF 已导出");
            }
        }
        catch (Exception exception) { viewModel.Status = $"岗位 PDF 导出失败：{exception.Message}"; }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsSummaryReady || viewModel.IsBusy) return;
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
            var posting = await EnsureCurrentPostingSavedAsync();
            if (posting is null)
            {
                return;
            }

            await jobPostingService.CreateApplicationAsync(posting, viewModel.SelectedCandidateProfile?.Id, "等待用户在平台页面确认提交");
            if (Uri.TryCreate(posting.Url, UriKind.Absolute, out var url)) BossWebView.Source = url;
            viewModel.Status = "已创建待确认投递记录，请在平台页面自行核对并提交。";
            App.GlobalToasts.ShowSuccess("已创建待确认投递");
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
        if (!viewModel.IsSummaryReady || viewModel.IsBusy) return;
        viewModel.IsBusy = true;
        try
        {
            var posting = await EnsureCurrentPostingSavedAsync();
            if (posting is null)
            {
                return;
            }

            await jobPostingService.QueueAsync(posting, viewModel.SelectedCandidateProfile?.Id);
            viewModel.Status = "岗位已加入人工复核队列。";
            App.GlobalToasts.ShowInfo("已加入复核队列");
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

    private JobPosting BuildCurrentPosting()
    {
        return new JobPosting
        {
            Platform = "Boss直聘",
            Url = viewModel.CurrentUrl,
            Title = viewModel.JobTitle,
            Company = viewModel.Company,
            City = viewModel.City,
            Salary = viewModel.Salary,
            Experience = viewModel.Experience,
            Education = viewModel.Education,
            Benefits = viewModel.Benefits,
            Description = viewModel.Description,
            Skills = viewModel.Tags
        };
    }

    private async Task<JobPosting?> EnsureCurrentPostingSavedAsync()
    {
        if (currentPosting is not null && viewModel.IsPostingSaved)
        {
            return currentPosting;
        }

        if (currentPostingDraft is null)
        {
            return null;
        }

        currentPosting = await jobPostingService.RecordViewedAsync(
            currentPostingDraft,
            viewModel.CurrentUrl,
            CancellationToken.None);
        viewModel.IsPostingSaved = true;
        viewModel.IsFavorite = currentPosting.IsFavorite;
        return currentPosting;
    }

    private async Task<JobPosting?> FindExistingPostingAsync(JobPostingDraft draft, string sourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(draft.ExternalId))
        {
            return await jobRepository.FindByExternalIdAsync("Boss直聘", draft.ExternalId, CancellationToken.None);
        }

        var url = Uri.TryCreate(draft.Url, UriKind.Absolute, out var canonicalUrl)
            ? canonicalUrl.AbsoluteUri
            : sourceUrl;
        var postings = await jobRepository.ListAsync(cancellationToken: CancellationToken.None);
        return postings.FirstOrDefault(posting => string.Equals(posting.Url, url, StringComparison.OrdinalIgnoreCase));
    }

    private async void OnImportClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ImportAsync();
        App.GlobalToasts.ShowInfo(viewModel.Status);
    }

    private async void OnMatchClick(object sender, RoutedEventArgs args)
    {
        await viewModel.MatchAsync();
        App.GlobalToasts.ShowInfo(viewModel.MatchStatus);
    }

    private void OnGenerateGreetingClick(object sender, RoutedEventArgs args)
    {
        viewModel.GenerateGreeting();
        viewModel.Status = "已根据所选简历和 JD 生成打招呼语。";
        App.GlobalToasts.ShowSuccess("打招呼语已生成");
    }

    private void OnCopyGreetingClick(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(viewModel.GreetingText))
        {
            viewModel.Status = "暂无可复制的打招呼语。";
            return;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(viewModel.GreetingText);
            Clipboard.SetContent(package);
            viewModel.Status = "已复制打招呼语。";
            App.GlobalToasts.ShowSuccess("已复制打招呼语");
        }
        catch (Exception exception)
        {
            viewModel.Status = $"复制打招呼语失败：{exception.Message}";
        }
    }

    private void OnOpenOnlineResumeClick(object sender, RoutedEventArgs args)
    {
        ((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow?.Navigate("resume");
    }

    private void OnExtractResumeClick(object sender, RoutedEventArgs args)
    {
        viewModel.ExtractResumeChunks();
    }

    private void OnCopyResumeChunkClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: ResumeChunk chunk })
        {
            return;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(chunk.Content);
            Clipboard.SetContent(package);
            viewModel.Status = $"已复制「{chunk.Title}」分块。";
            App.GlobalToasts.ShowSuccess("简历分块已复制");
        }
        catch (Exception exception)
        {
            viewModel.Status = $"复制简历分块失败：{exception.Message}";
        }
    }

    private async void OnFillResumeChunkClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: ResumeChunk chunk } || BossWebView.CoreWebView2 is null)
        {
            viewModel.Status = "请先打开可编辑的 BOSS 页面。";
            return;
        }

        try
        {
            var result = await FillResumeChunkAsync(chunk);
            viewModel.Status = result?.Filled == true
                ? $"已将「{chunk.Title}」填入当前页面{(string.IsNullOrWhiteSpace(result.Field) ? "" : $"（{result.Field}）")}。请检查后再保存。"
                : result?.Reason ?? $"当前页面没有找到「{chunk.Title}」对应的字段。";
            if (result?.Filled == true)
            {
                App.GlobalToasts.ShowInfo("简历分块已填入页面");
            }
        }
        catch (Exception exception)
        {
            viewModel.Status = $"填入「{chunk.Title}」失败：{exception.Message}";
        }
    }

    private async Task<ResumeFillResult?> FillResumeChunkAsync(ResumeChunk chunk)
    {
        var fieldKey = JsonSerializer.Serialize(chunk.FieldKey);
        var content = JsonSerializer.Serialize(chunk.Content);
        var script = $$"""
            (() => {
              const key = {{fieldKey}};
              const value = {{content}};
              const aliases = {
                name: ['姓名', '名字', '真实姓名', 'realname', 'resume-name'],
                phone: ['手机号', '手机', '电话', 'phone', 'mobile'],
                email: ['邮箱', '电子邮箱', 'email'],
                location: ['所在地', '城市', '地址', 'location', 'city'],
                intention: ['求职意向', '期望职位', '目标岗位', '职位名称', 'intention', 'job-title'],
                education: ['教育经历', '教育背景', '学历', '学校', 'education'],
                experience: ['工作经历', '工作经验', '工作内容', 'experience', 'work'],
                project: ['项目经历', '项目经验', 'project'],
                skills: ['专业技能', '技能特长', '技能', '证书', 'skills'],
                summary: ['自我介绍', '个人简介', '自我评价', 'summary', 'profile']
              };
              const hostname = location.hostname.toLowerCase();
              if (!hostname.includes('zhipin.com') && !hostname.includes('bossfind.local')) {
                return JSON.stringify({ filled: false, fieldKey: key, reason: '当前页面不是 BOSS 页面。' });
              }

              const getLabel = (element) => [
                element.getAttribute('placeholder'),
                element.getAttribute('aria-label'),
                element.getAttribute('name'),
                element.id,
                element.closest('label')?.innerText,
                element.previousElementSibling?.innerText
              ].filter(Boolean).join(' ').toLowerCase();
              const acceptedAliases = (aliases[key] || []).map(alias => alias.toLowerCase());
              const candidates = Array.from(document.querySelectorAll("input, textarea, [contenteditable='true']"))
                .map(element => ({ element, label: getLabel(element) }))
                .filter(item => acceptedAliases.some(alias => item.label.includes(alias)));
              if (candidates.length === 0) {
                return JSON.stringify({ filled: false, fieldKey: key, reason: '当前页面没有找到对应字段，请确认已打开 BOSS 在线简历编辑页。' });
              }

              const target = candidates[0].element;
              if (target.isContentEditable) {
                target.textContent = value;
              } else {
                const prototype = target instanceof HTMLTextAreaElement
                  ? HTMLTextAreaElement.prototype
                  : HTMLInputElement.prototype;
                const setter = Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
                if (setter) {
                  setter.call(target, value);
                } else {
                  target.value = value;
                }
              }
              target.dispatchEvent(new Event('input', { bubbles: true }));
              target.dispatchEvent(new Event('change', { bubbles: true }));
              target.focus();
              return JSON.stringify({ filled: true, fieldKey: key, reason: '', field: getLabel(target) });
            })();
            """;

        var serializedResult = await BossWebView.ExecuteScriptAsync(script);
        var resultJson = JsonSerializer.Deserialize<string>(serializedResult) ?? "{}";
        return JsonSerializer.Deserialize<ResumeFillResult>(resultJson, MetadataJsonOptions);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        isPageActive = false;
        Dispose();
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
    }

    public void Dispose()
    {
        initializationCancellation?.Cancel();
        initializationCancellation?.Dispose();
        initializationCancellation = null;
    }
}
