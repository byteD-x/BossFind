using BossFind.Application.Insights;
using BossFind.Domain.Entities;

namespace BossFind.Application.Tests;

public sealed class LocalJobInsightServiceTests
{
    [Fact]
    public async Task CreateAsync_reports_missing_job_fields_without_external_service()
    {
        var service = new LocalJobInsightService();

        var insight = await service.CreateAsync(new JobPosting
        {
            Title = "后端工程师",
            Company = "示例科技",
            City = "上海",
            Skills = "C#、.NET"
        });

        Assert.Equal("本地规则建议", insight.Provider);
        Assert.Contains("示例科技", insight.Summary, StringComparison.Ordinal);
        Assert.Equal(4, insight.Suggestions.Count);
    }
}
