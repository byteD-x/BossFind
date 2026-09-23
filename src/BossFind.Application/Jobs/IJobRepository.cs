using BossFind.Domain.Entities;

namespace BossFind.Application.Jobs;

public interface IJobRepository
{
    Task<JobPosting?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobPosting?> FindByExternalIdAsync(string platform, string externalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobPosting>> ListAsync(bool favoritesOnly = false, CancellationToken cancellationToken = default);

    Task<JobPosting> UpsertAsync(JobPosting posting, CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default);

    Task<JobApplication> AddApplicationAsync(JobApplication application, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobApplication>> ListApplicationsAsync(CancellationToken cancellationToken = default);

    Task UpdateApplicationStatusAsync(Guid id, JobApplicationStatus status, CancellationToken cancellationToken = default);

    Task<JobAutomationTask> AddAutomationTaskAsync(JobAutomationTask task, CancellationToken cancellationToken = default);

    Task<JobAutomationTask?> FindActiveAutomationTaskAsync(Guid jobPostingId, Guid? candidateProfileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobAutomationTask>> ListAutomationTasksAsync(CancellationToken cancellationToken = default);

    Task UpdateAutomationTaskStatusAsync(Guid id, JobAutomationTaskStatus status, CancellationToken cancellationToken = default);
}
