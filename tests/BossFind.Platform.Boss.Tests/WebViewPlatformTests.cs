using BossFind.Platform.Boss.Playwright;
using BossFind.Platform.Boss.Parsing;
using BossFind.Platform.Boss.WebView;

namespace BossFind.Platform.Boss.Tests;

public sealed class WebViewPlatformTests
{
    [Fact]
    public void Profile_path_is_isolated_under_local_app_data()
    {
        var path = BossWebViewProfilePaths.GetBossProfileDirectory("C:\\Users\\test\\AppData\\Local");

        Assert.Equal(
            "C:\\Users\\test\\AppData\\Local\\BossFind\\WebView2\\BossProfile",
            path);
    }

    [Fact]
    public void Profile_cleaner_deletes_only_the_boss_profile_directory()
    {
        var localApplicationData = Path.Combine(Path.GetTempPath(), $"bossfind-profile-{Guid.NewGuid():N}");
        var profilePath = BossWebViewProfilePaths.GetBossProfileDirectory(localApplicationData);
        Directory.CreateDirectory(profilePath);
        File.WriteAllText(Path.Combine(profilePath, "user-data.txt"), "fixture");

        try
        {
            Assert.True(BossWebViewProfileCleaner.Clear(localApplicationData));
            Assert.False(Directory.Exists(profilePath));
            Assert.False(BossWebViewProfileCleaner.Clear(localApplicationData));
        }
        finally
        {
            if (Directory.Exists(localApplicationData))
            {
                Directory.Delete(localApplicationData, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Cdp_verifier_rejects_non_loopback_endpoint_before_connecting()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            PlaywrightCdpVerifier.VerifyAsync(new Uri("https://example.com:9222")));

        Assert.Contains("回环", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_probe_returns_a_status_without_throwing()
    {
        var status = WebView2RuntimeProbe.GetStatus();

        Assert.NotNull(status);
        Assert.True(status.IsAvailable || !string.IsNullOrWhiteSpace(status.Error));
    }

    [Fact]
    public void Job_summary_parser_reads_fixture_fields_and_tags()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "job-detail.html"));

        var summary = BossJobSummaryParser.Parse(html);

        Assert.Equal("高级 C# 工程师", summary.Title);
        Assert.Equal("示例科技", summary.Company);
        Assert.Equal("上海", summary.City);
        Assert.Equal([".NET", "WinUI 3", "SQLite"], summary.Tags);
        Assert.Equal("25-40K · 14薪", summary.Salary);
        Assert.Equal("3-5年", summary.Experience);
        Assert.Equal("本科", summary.Education);
        Assert.Equal(["弹性工作"], summary.BenefitList);
        Assert.Equal("负责 Windows 客户端和本地数据能力建设。", summary.Description);
        Assert.Equal("fixture-csharp-senior", summary.ExternalId);
    }

    [Fact]
    public void Job_summary_parser_returns_empty_values_for_missing_fields()
    {
        const string html = "<article data-testid='job-detail'><h1 data-testid='job-title'>  仅有标题 </h1><span class='tag'> C# </span><span class='tag'>C#</span></article>";

        var summary = BossJobSummaryParser.Parse(html);

        Assert.Equal("仅有标题", summary.Title);
        Assert.Empty(summary.Company);
        Assert.Empty(summary.City);
        Assert.Equal(["C#"], summary.Tags);
    }

    [Fact]
    public void Job_summary_parser_reads_common_boss_dom_classes()
    {
        const string html = """
            <html><head><link rel="canonical" href="https://www.zhipin.com/job_detail/abc.html"></head>
            <body><div class="job-primary">
              <h1 class="name">后端开发工程师</h1>
              <div class="company-name">真实科技</div>
              <span class="location-address">杭州</span>
              <span class="job-name">20-35K</span>
              <div class="job-sec-text">负责服务端开发与维护</div>
              <ul class="tag-list"><li class="tag">C#</li><li class="tag">.NET</li></ul>
            </div></body></html>
            """;

        var summary = BossJobSummaryParser.Parse(html);

        Assert.Equal("后端开发工程师", summary.Title);
        Assert.Equal("真实科技", summary.Company);
        Assert.Equal("杭州", summary.City);
        Assert.Equal("20-35K", summary.Salary);
        Assert.Equal("负责服务端开发与维护", summary.Description);
        Assert.Equal(["C#", ".NET"], summary.Tags);
        Assert.Equal("https://www.zhipin.com/job_detail/abc.html", summary.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<not-valid")]
    [InlineData("<!doctype html><body><div>无岗位摘要</div></body>")]
    public void Job_summary_parser_handles_empty_or_invalid_html(string html)
    {
        var summary = BossJobSummaryParser.Parse(html);

        Assert.Equal(string.Empty, summary.Title);
        Assert.Equal(string.Empty, summary.Company);
        Assert.Equal(string.Empty, summary.City);
        Assert.Empty(summary.Tags);
    }

    [Fact]
    public void Job_search_result_parser_reads_cards_and_deduplicates_external_ids()
    {
        const string html = """
            <html><body>
              <div class="job-card-wrapper" data-jobid="job-1">
                <a href="/job_detail/job-1.html"><span class="job-name">后端开发工程师</span></a>
                <div class="company-name">示例科技</div>
                <span class="job-area">上海·浦东新区</span>
                <span class="job-salary">20-35K·13薪</span>
                <span class="job-limit">3-5年 本科</span>
                <ul class="job-tags"><li>C#</li><li>.NET</li></ul>
              </div>
              <div class="job-card-wrapper" data-jobid="job-1">
                <a href="/job_detail/job-1.html"><span class="job-name">重复岗位</span></a>
              </div>
              <div class="job-card-wrapper">
                <a href="/job_detail/job-2.html"><span class="job-name">数据分析师</span></a>
                <div class="company-name">另一家公司</div>
                <span class="job-area">杭州</span>
              </div>
            </body></html>
            """;

        var results = BossJobSearchResultParser.Parse(html, "https://www.zhipin.com/job/list.html?page=1");

        Assert.Equal(2, results.Count);
        Assert.Equal("后端开发工程师", results[0].Title);
        Assert.Equal("上海", results[0].City);
        Assert.Equal("本科", results[0].Education);
        Assert.Equal("3-5年", results[0].Experience);
        Assert.Equal(["C#", ".NET"], results[0].Tags);
        Assert.Equal("job-1", results[0].ExternalId);
        Assert.Equal("https://www.zhipin.com/job_detail/job-1.html", results[0].Url);
        Assert.Equal("job-2", results[1].ExternalId);
    }

    [Fact]
    public void Job_search_result_parser_ignores_cards_without_job_links()
    {
        const string html = "<div class='job-card-wrapper'><span class='job-name'>推荐内容</span></div>";

        var results = BossJobSearchResultParser.Parse(html);

        Assert.Empty(results);
    }
}
