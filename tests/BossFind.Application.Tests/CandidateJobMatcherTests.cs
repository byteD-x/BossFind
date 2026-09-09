using BossFind.Application.Matching;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Tests;

public sealed class CandidateJobMatcherTests
{
    [Fact]
    public void Match_reports_matched_and_missing_skills()
    {
        var candidate = new CandidateProfile
        {
            Headline = "后端工程师",
            Facts =
            [
                new CandidateFact
                {
                    Category = CandidateFactCategory.Skill,
                    Content = "熟悉 C# 和 SQLite"
                }
            ]
        };
        var requirement = new JobRequirement(
            "后端工程师",
            null,
            null,
            ["C#", " .NET ", "SQLite"]);

        var result = CandidateJobMatcher.Match(candidate, requirement);

        Assert.Equal(["C#", "SQLite"], result.MatchedSkills);
        Assert.Equal([".NET"], result.MissingSkills);
        Assert.InRange(result.Score, 0m, 1m);
        Assert.Contains("技能命中 2/3", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_deduplicates_skills_and_ignores_case_and_whitespace()
    {
        var candidate = new CandidateProfile
        {
            Headline = "C# 工程师",
            Facts = [new CandidateFact { Content = ".NET 8" }]
        };

        var result = CandidateJobMatcher.Match(
            candidate,
            new JobRequirement("工程师", null, null, [" C# ", "c#", ".NET"]));

        Assert.Equal(["C#", ".NET"], result.MatchedSkills);
        Assert.Empty(result.MissingSkills);
        Assert.Equal(1m, result.Score);
    }

    [Fact]
    public void Match_rejects_blank_job_title()
    {
        var exception = Assert.Throws<ArgumentException>(() => CandidateJobMatcher.Match(
            new CandidateProfile(),
            new JobRequirement("  ", null, null, null)));

        Assert.Contains("岗位标题不能为空", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_without_optional_requirements_returns_zero_score()
    {
        var result = CandidateJobMatcher.Match(
            new CandidateProfile { Headline = "任意职位" },
            new JobRequirement("完全不同的职位", null, null, null));

        Assert.Equal(0m, result.Score);
        Assert.Empty(result.MatchedSkills);
        Assert.Empty(result.MissingSkills);
        Assert.Contains("未设置城市要求", result.Explanation, StringComparison.Ordinal);
    }
}
