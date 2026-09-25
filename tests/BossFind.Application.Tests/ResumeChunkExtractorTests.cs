using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Tests;

public sealed class ResumeChunkExtractorTests
{
    [Fact]
    public void Extract_uses_profile_fields_and_groups_facts_by_resume_section()
    {
        var profile = new CandidateProfile
        {
            Name = "林晓",
            Headline = "后端开发工程师",
            Email = "lin@example.com",
            Facts =
            [
                new CandidateFact { Category = CandidateFactCategory.Education, Content = "上海大学 · 计算机科学" },
                new CandidateFact { Category = CandidateFactCategory.Experience, Content = "负责订单服务重构" },
                new CandidateFact { Category = CandidateFactCategory.Skill, Content = "C#、.NET、SQL" }
            ]
        };

        var chunks = ResumeChunkExtractor.Extract(profile);

        Assert.Equal("林晓", Find(chunks, "name").Content);
        Assert.Equal("后端开发工程师", Find(chunks, "intention").Content);
        Assert.Equal("上海大学 · 计算机科学", Find(chunks, "education").Content);
        Assert.Equal("负责订单服务重构", Find(chunks, "experience").Content);
        Assert.Equal("C#、.NET、SQL", Find(chunks, "skills").Content);
    }

    [Fact]
    public void Extract_prefers_labeled_values_and_sections_from_pasted_resume()
    {
        var profile = new CandidateProfile { Name = "旧姓名", Headline = "旧岗位" };
        var sourceText = """
            姓名：周宁
            手机：13800000000
            求职意向：数据分析师
            技能：SQL、Python

            教育背景
            南京大学 · 统计学

            项目经历
            搭建经营看板，推动周报自动化
            """;

        var chunks = ResumeChunkExtractor.Extract(profile, sourceText);

        Assert.Equal("周宁", Find(chunks, "name").Content);
        Assert.Equal("13800000000", Find(chunks, "phone").Content);
        Assert.Equal("数据分析师", Find(chunks, "intention").Content);
        Assert.Equal("南京大学 · 统计学", Find(chunks, "education").Content);
        Assert.Equal("搭建经营看板，推动周报自动化", Find(chunks, "project").Content);
        Assert.Equal("SQL、Python", Find(chunks, "skills").Content);
    }

    private static ResumeChunk Find(IReadOnlyList<ResumeChunk> chunks, string fieldKey) =>
        Assert.Single(chunks, chunk => chunk.FieldKey == fieldKey);
}
