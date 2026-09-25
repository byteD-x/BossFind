using BossFind.Domain.Entities;

namespace BossFind.Application.Insights;

public sealed record JobPostingQuality(
    int CompletenessScore,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<string> RiskFlags)
{
    public string ScoreLabel => $"岗位信息完整度 {CompletenessScore}%";

    public string MissingFieldsLabel => MissingFields.Count == 0
        ? "关键字段已填写。"
        : $"待确认：{string.Join("、", MissingFields)}";

    public string RiskFlagsLabel => RiskFlags.Count == 0
        ? "暂未发现明显风险信号。"
        : string.Join("；", RiskFlags);
}

public static class JobPostingQualityAnalyzer
{
    private static readonly (string Label, Func<JobPosting, string> Value)[] CompletenessFields =
    [
        ("薪资", posting => posting.Salary),
        ("经验要求", posting => posting.Experience),
        ("学历要求", posting => posting.Education),
        ("岗位描述", posting => posting.Description),
        ("技能要求", posting => posting.Skills),
        ("福利", posting => posting.Benefits)
    ];

    public static JobPostingQuality Analyze(JobPosting posting)
    {
        ArgumentNullException.ThrowIfNull(posting);

        var missingFields = CompletenessFields
            .Where(field => string.IsNullOrWhiteSpace(field.Value(posting)))
            .Select(field => field.Label)
            .ToArray();
        var completenessScore = (int)Math.Round(
            (CompletenessFields.Length - missingFields.Length) * 100m / CompletenessFields.Length,
            MidpointRounding.AwayFromZero);

        var source = string.Join(
            ' ',
            new[] { posting.Title, posting.Company, posting.Description, posting.Benefits, posting.Skills }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var riskFlags = new List<string>();
        if (ContainsAny(source, "培训贷", "培训费", "报名费", "押金", "中介费", "收费"))
        {
            riskFlags.Add("页面出现收费或培训相关词，建议确认是否需要付费。");
        }

        if (ContainsAny(source, "外包", "驻场", "劳务派遣"))
        {
            riskFlags.Add("页面可能涉及外包或派遣，建议确认实际用工主体。");
        }

        if (string.IsNullOrWhiteSpace(posting.Salary)
            || ContainsAny(posting.Salary, "面议", "不限"))
        {
            riskFlags.Add("薪资范围不明确，建议在沟通前确认。");
        }

        if (ContainsAny(source, "大小周", "单休", "无固定加班", "抗压能力强"))
        {
            riskFlags.Add("页面包含工作安排或加班相关信号，建议继续核实。");
        }

        return new JobPostingQuality(completenessScore, missingFields, riskFlags);
    }

    private static bool ContainsAny(string? value, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
