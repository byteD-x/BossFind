using BossFind.App.ViewModels;
using BossFind.Application.Common;
using BossFind.Application.Jobs;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Ui.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_uses_profile_facts_and_reports_readiness()
    {
        var repository = new InMemoryRepository();
        repository.Profiles.AddRange(
        [
            new CandidateProfile
            {
                Name = "林晓",
                UpdatedAtUtc = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc),
                Facts =
                [
                    new CandidateFact { Category = CandidateFactCategory.Skill, IsConfirmed = true },
                    new CandidateFact { Category = CandidateFactCategory.Experience, IsConfirmed = false }
                ]
            },
            new CandidateProfile
            {
                Name = "周宁",
                UpdatedAtUtc = new DateTime(2026, 9, 9, 8, 30, 0, DateTimeKind.Utc),
                Facts = [new CandidateFact { IsConfirmed = true }]
            }
        ]);
        var posting = new JobPosting { Title = "后端工程师", Company = "示例科技" };
        repository.Postings.Add(posting);
        repository.Applications.Add(new JobApplication
        {
            JobPosting = posting,
            JobPostingId = posting.Id,
            Status = JobApplicationStatus.ReadyForReview
        });
        repository.Tasks.Add(new JobAutomationTask
        {
            JobPosting = posting,
            JobPostingId = posting.Id,
            Status = JobAutomationTaskStatus.Queued
        });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();

        Assert.Equal("工作台数据已更新。", viewModel.StatusMessage);
        Assert.Equal("2", viewModel.ProfileCount);
        Assert.Equal("2", viewModel.ConfirmedFactCount);
        Assert.Equal("1", viewModel.SavedJobCount);
        Assert.Equal("2", viewModel.PendingActionCount);
        Assert.Single(viewModel.RecentPostings);
        Assert.Equal(0, repository.GetProfileCallCount);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoadAsync_reports_empty_workspace()
    {
        var viewModel = CreateViewModel(new InMemoryRepository());

        await viewModel.LoadAsync();

        Assert.Equal("工作台数据已更新。", viewModel.StatusMessage);
        Assert.Equal("0", viewModel.ProfileCount);
        Assert.Equal("0", viewModel.ConfirmedFactCount);
        Assert.Equal("0", viewModel.SavedJobCount);
        Assert.Equal("0", viewModel.PendingActionCount);
        Assert.Contains("还没有岗位记录", viewModel.EmptyJobsMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_reports_cancellation_and_resets_busy_state()
    {
        var viewModel = CreateViewModel(new InMemoryRepository());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.LoadAsync(cancellation.Token);

        Assert.Equal("刷新已取消。", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    private static DashboardViewModel CreateViewModel(InMemoryRepository repository)
    {
        var service = new CandidateProfileService(
            new FixedClock(new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
            repository);
        return new DashboardViewModel(service, repository);
    }

    private sealed class InMemoryRepository : ICandidateProfileRepository, IJobRepository
    {
        public List<CandidateProfile> Profiles { get; } = [];

        public List<JobPosting> Postings { get; } = [];

        public List<JobApplication> Applications { get; } = [];

        public List<JobAutomationTask> Tasks { get; } = [];

        public int GetProfileCallCount { get; private set; }

        public Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(
            string? searchText,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<CandidateProfile>>(Profiles);
        }

        public Task<CandidateProfile?> GetProfileAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetProfileCallCount++;
            return Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == profileId));
        }

        public Task AddProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddFactAsync(CandidateFact fact, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateFactAsync(CandidateFact fact, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteFactAsync(Guid factId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobPosting?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Postings.SingleOrDefault(posting => posting.Id == id));

        public Task<JobPosting?> FindByExternalIdAsync(string platform, string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Postings.SingleOrDefault(posting => posting.Platform == platform && posting.ExternalId == externalId));

        public Task<IReadOnlyList<JobPosting>> ListAsync(bool favoritesOnly = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobPosting>>(Postings.Where(posting => !favoritesOnly || posting.IsFavorite).ToArray());

        public Task<JobPosting> UpsertAsync(JobPosting posting, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobApplication> AddApplicationAsync(JobApplication application, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobApplication>> ListApplicationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobApplication>>(Applications);

        public Task UpdateApplicationStatusAsync(Guid id, JobApplicationStatus status, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobAutomationTask> AddAutomationTaskAsync(JobAutomationTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobAutomationTask?> FindActiveAutomationTaskAsync(Guid jobPostingId, Guid? candidateProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tasks.LastOrDefault(task => task.JobPostingId == jobPostingId
                && task.CandidateProfileId == candidateProfileId
                && task.Status is JobAutomationTaskStatus.Queued or JobAutomationTaskStatus.InReview));

        public Task<IReadOnlyList<JobAutomationTask>> ListAutomationTasksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobAutomationTask>>(Tasks);

        public Task UpdateAutomationTaskStatusAsync(Guid id, JobAutomationTaskStatus status, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTime value) : IClock
    {
        public DateTime UtcNow { get; } = value;
    }
}
