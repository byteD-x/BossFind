using BossFind.Application.Insights;
using BossFind.Domain.Entities;

namespace BossFind.Application.Tests;

public sealed class JobPostingQualityAnalyzerTests
{
    [Fact]
    public void Analyze_reports_missing_fields_and_risk_signals()
    {
        var result = JobPostingQualityAnalyzer.Analyze(new JobPosting
        {
            Title = "后端工程师",
            Company = "示例外包公司",
            City = "上海",
            Description = "接受培训贷，大小周，负责服务开发。"
        });

        Assert.Equal(17, result.CompletenessScore);
        Assert.Contains("薪资", result.MissingFields);
        Assert.Contains(result.RiskFlags, flag => flag.Contains("收费", StringComparison.Ordinal));
        Assert.Contains(result.RiskFlags, flag => flag.Contains("外包", StringComparison.Ordinal));
        Assert.Contains(result.RiskFlags, flag => flag.Contains("加班", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_marks_a_complete_posting_without_risk_signals()
    {
        var result = JobPostingQualityAnalyzer.Analyze(new JobPosting
        {
            Title = "后端工程师",
            Company = "示例科技",
            City = "上海",
            Salary = "20-30K·13薪",
            Experience = "3-5年",
            Education = "本科",
            Benefits = "五险一金",
            Skills = "C#、.NET",
            Description = "负责后端服务开发。"
        });

        Assert.Equal(100, result.CompletenessScore);
        Assert.Empty(result.MissingFields);
        Assert.Empty(result.RiskFlags);
    }
}
