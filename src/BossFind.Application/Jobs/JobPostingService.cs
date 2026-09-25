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
    private const string PlatformName = "Boss直聘";

    public async Task<IReadOnlyList<JobPosting>> RecordViewedBatchAsync(
        IEnumerable<JobPostingDraft> summaries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summaries);

        var postings = new List<JobPosting>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var summary in summaries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(summary);
            if (!TryGetBatchKey(summary, out var key) || !seenKeys.Add(key))
            {
                continue;
            }

            postings.Add(await RecordViewedAsync(summary, summary.Url, cancellationToken));
        }

        return postings;
    }

    public async Task<JobPosting> RecordViewedAsync(
        JobPostingDraft summary,
        string url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var postingUrl = NormalizeUrl(summary.Url) ?? NormalizeUrl(url)
            ?? throw new ArgumentException("岗位链接必须是有效的 HTTP 或 HTTPS 地址。", nameof(url));
        var externalId = string.IsNullOrWhiteSpace(summary.ExternalId)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(postingUrl)))
            : summary.ExternalId.Trim();
        var posting = await repository.FindByExternalIdAsync(PlatformName, externalId, cancellationToken);
        if (posting is null)
        {
            posting = new JobPosting { FirstSeenAtUtc = DateTime.UtcNow, ViewCount = 1 };
        }
        else
        {
            posting.ViewCount++;
        }

        posting.Platform = PlatformName;
        posting.ExternalId = externalId;
        posting.Url = postingUrl;
        posting.Title = NormalizeText(summary.Title);
        posting.Company = NormalizeText(summary.Company);
        posting.City = NormalizeText(summary.City);
        posting.Salary = NormalizeText(summary.Salary);
        posting.Experience = NormalizeText(summary.Experience);
        posting.Education = NormalizeText(summary.Education);
        posting.Benefits = JoinDistinct(summary.Benefits);
        posting.Description = NormalizeText(summary.Description);
        posting.Skills = JoinDistinct(summary.Skills);
        posting.LastViewedAtUtc = DateTime.UtcNow;
        return await repository.UpsertAsync(posting, cancellationToken);
    }

    private static bool TryGetBatchKey(JobPostingDraft summary, out string key)
    {
        var url = NormalizeUrl(summary.Url);
        if (url is null)
        {
            key = string.Empty;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(summary.ExternalId))
        {
            key = $"id:{summary.ExternalId.Trim()}";
            return true;
        }

        key = $"url:{url}";
        return true;
    }

    private static string? NormalizeUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? uri.AbsoluteUri
                : null;
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    private static string JoinDistinct(IReadOnlyList<string>? values)
    {
        return string.Join(
            "、",
            (values ?? [])
                .Select(NormalizeText)
                .Where(static value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase));
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
