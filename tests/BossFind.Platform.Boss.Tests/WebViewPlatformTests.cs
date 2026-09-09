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
}
