using BossFind.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace BossFind.Application.Jobs;

public sealed record JobPostingDraft(
    string Title,
    string Company,
    string City,
    IReadOnlyList<string> Skills,
    string Salary = "",
    string Experience = "",
    string Education = "",
    IReadOnlyList<string>? Benefits = null,
    string Description = "",
    string ExternalId = "",
    string Url = "");

public sealed class JobPostingService(IJobRepository repository)
{
    public async Task<JobPosting> RecordViewedAsync(
        JobPostingDraft summary,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var postingUrl = Uri.TryCreate(summary.Url, UriKind.Absolute, out var summaryUrl)
            && (summaryUrl.Scheme == Uri.UriSchemeHttp || summaryUrl.Scheme == Uri.UriSchemeHttps)
                ? summaryUrl.AbsoluteUri
                : url;
        var externalId = string.IsNullOrWhiteSpace(summary.ExternalId)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(postingUrl)))
            : summary.ExternalId.Trim();
        var posting = await repository.FindByExternalIdAsync("Boss直聘", externalId, cancellationToken);
        if (posting is null)
        {
            posting = new JobPosting { FirstSeenAtUtc = DateTime.UtcNow, ViewCount = 1 };
        }
        else
        {
            posting.ViewCount++;
        }

        posting.Platform = "Boss直聘";
        posting.ExternalId = externalId;
        posting.Url = postingUrl;
        posting.Title = summary.Title;
        posting.Company = summary.Company;
        posting.City = summary.City;
        posting.Salary = summary.Salary;
        posting.Experience = summary.Experience;
        posting.Education = summary.Education;
        posting.Benefits = string.Join("、", summary.Benefits ?? []);
        posting.Description = summary.Description;
        posting.Skills = string.Join("、", summary.Skills);
        posting.LastViewedAtUtc = DateTime.UtcNow;
        return await repository.UpsertAsync(posting, cancellationToken);
    }

    public async Task SetFavoriteAsync(JobPosting posting, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await repository.SetFavoriteAsync(posting.Id, isFavorite, cancellationToken);
        posting.IsFavorite = isFavorite;
    }

    // 只设置外键而不设置导航属性：posting 由 RecordViewedAsync 返回，其所属 DbContext 已经释放，
    // 是游离实体。若把游离实体挂到导航属性上，Add 会把整个对象图标记为 Added 并重复插入 JobPostings，
    // 触发 (Platform, ExternalId) 唯一索引冲突。
    public Task<JobApplication> CreateApplicationAsync(
        JobPosting posting,
        Guid? candidateProfileId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        return repository.AddApplicationAsync(new JobApplication
        {
            JobPostingId = posting.Id,
            CandidateProfileId = candidateProfileId,
            Status = JobApplicationStatus.ReadyForReview,
            Note = note?.Trim() ?? string.Empty
        }, cancellationToken);
    }

    public Task<JobAutomationTask> QueueAsync(
        JobPosting posting,
        Guid? candidateProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        return QueueCoreAsync(posting, candidateProfileId, cancellationToken);
    }

    private async Task<JobAutomationTask> QueueCoreAsync(
        JobPosting posting,
        Guid? candidateProfileId,
        CancellationToken cancellationToken)
    {
        var activeTask = await repository.FindActiveAutomationTaskAsync(posting.Id, candidateProfileId, cancellationToken);
        if (activeTask is not null)
        {
            return activeTask;
        }

        return await repository.AddAutomationTaskAsync(new JobAutomationTask
        {
            JobPostingId = posting.Id,
            CandidateProfileId = candidateProfileId
        }, cancellationToken);
    }

    public Task UpdateApplicationStatusAsync(
        Guid applicationId,
        JobApplicationStatus status,
        CancellationToken cancellationToken = default)
    {
        return repository.UpdateApplicationStatusAsync(applicationId, status, cancellationToken);
    }

    public Task UpdateAutomationTaskStatusAsync(
        Guid taskId,
        JobAutomationTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        return repository.UpdateAutomationTaskStatusAsync(taskId, status, cancellationToken);
    }
}
