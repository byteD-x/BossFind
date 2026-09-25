using BossFind.Domain.Entities;

namespace BossFind.Application.Insights;

public sealed record JobInsight(string Summary, IReadOnlyList<string> Suggestions, string Provider);

public interface IJobInsightService
{
    Task<JobInsight> CreateAsync(JobPosting posting, CancellationToken cancellationToken = default);
}

public sealed class LocalJobInsightService : IJobInsightService
{
    public Task<JobInsight> CreateAsync(JobPosting posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        cancellationToken.ThrowIfCancellationRequested();
        var suggestions = new List<string>();
        if (string.IsNullOrWhiteSpace(posting.Salary)) suggestions.Add("建议先确认薪资范围和绩效结构。");
        if (string.IsNullOrWhiteSpace(posting.Experience)) suggestions.Add("建议确认岗位经验要求及实际负责范围。");
        if (string.IsNullOrWhiteSpace(posting.Education)) suggestions.Add("建议确认学历要求是否为硬性条件。");
        if (string.IsNullOrWhiteSpace(posting.Description)) suggestions.Add("岗位职责尚未提取，建议在平台页面核对具体工作内容。");
        if (suggestions.Count == 0) suggestions.Add("关键信息已填写，可继续核对岗位职责和简历匹配结果。");

        var details = new[] { posting.Title, posting.Company, posting.City }
            .Where(static value => !string.IsNullOrWhiteSpace(value));
        var summary = string.Join(" · ", details);
        if (!string.IsNullOrWhiteSpace(posting.Skills))
        {
            summary += $"。技能要求：{posting.Skills}。";
        }

        return Task.FromResult(new JobInsight(summary, suggestions, "本地规则建议"));
    }
}
