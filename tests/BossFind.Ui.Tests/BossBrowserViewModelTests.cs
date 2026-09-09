using BossFind.App.ViewModels;
using BossFind.Application.Common;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;

namespace BossFind.Ui.Tests;

public sealed class BossBrowserViewModelTests
{
    [Fact]
    public void SetJobSummary_with_empty_title_marks_summary_not_ready()
    {
        var (viewModel, _) = CreateViewModel();

        viewModel.SetJobSummary(string.Empty, "示例科技", "上海", ["C#"]);

        Assert.False(viewModel.IsSummaryReady);
    }

    [Fact]
    public async Task ImportAsync_creates_profile_and_reports_success()
    {
        var (viewModel, repository) = CreateViewModel();
        viewModel.CandidateName = "  林晓  ";
        viewModel.SetJobSummary("高级 C# 工程师", "示例科技", "上海", ["C#", ".NET"]);

        await viewModel.ImportAsync();

        var profile = Assert.Single(repository.Profiles);
        Assert.Equal("林晓", profile.Name);
        Assert.Equal("已导入候选人档案：林晓", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.Equal("C#、.NET", viewModel.Tags);
    }

    [Fact]
    public async Task ImportAsync_reports_empty_candidate_name_failure()
    {
        var (viewModel, repository) = CreateViewModel();
        viewModel.CandidateName = "   ";
        viewModel.SetJobSummary("高级 C# 工程师", "示例科技", "上海", []);

        await viewModel.ImportAsync();

        Assert.Contains("候选人姓名不能为空", viewModel.Status, StringComparison.Ordinal);
        Assert.Empty(repository.Profiles);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ImportAsync_reports_cancellation_and_resets_busy_state()
    {
        var (viewModel, repository) = CreateViewModel();
        viewModel.SetJobSummary("高级 C# 工程师", "示例科技", "上海", []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.ImportAsync(cancellation.Token);

        Assert.Equal("导入已取消。", viewModel.Status);
        Assert.Empty(repository.Profiles);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ImportAsync_ignores_reentrant_call_while_busy()
    {
        var (viewModel, repository) = CreateViewModel();
        repository.BlockProfileAdd = true;
        viewModel.SetJobSummary("高级 C# 工程师", "示例科技", "上海", []);

        var firstImport = viewModel.ImportAsync();
        await repository.ProfileAddStarted.Task;

        await viewModel.ImportAsync();

        Assert.True(viewModel.IsBusy);
        Assert.Equal(1, repository.AddProfileCallCount);

        repository.ReleaseProfileAdd.TrySetResult(true);
        await firstImport;

        Assert.Equal("已导入候选人档案：待补充候选人", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.Single(repository.Profiles);
    }

    private static (BossBrowserViewModel ViewModel, InMemoryRepository Repository) CreateViewModel()
    {
        var repository = new InMemoryRepository();
        var profileService = new CandidateProfileService(
            new FixedClock(new DateTime(2026, 9, 8, 1, 0, 0, DateTimeKind.Utc)),
            repository);
        return (new BossBrowserViewModel(new JobCandidateImportService(profileService)), repository);
    }

    private sealed class InMemoryRepository : ICandidateProfileRepository
    {
        public List<CandidateProfile> Profiles { get; } = [];

        public List<CandidateFact> Facts { get; } = [];

        public bool BlockProfileAdd { get; set; }

        public int AddProfileCallCount { get; private set; }

        public TaskCompletionSource<bool> ProfileAddStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseProfileAdd { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            return Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == profileId));
        }

        public Task AddProfileAsync(
            CandidateProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddProfileCallCount++;
            if (!BlockProfileAdd)
            {
                Profiles.Add(profile);
                return Task.CompletedTask;
            }

            ProfileAddStarted.TrySetResult(true);
            return AddProfileAfterReleaseAsync(profile, cancellationToken);
        }

        public Task UpdateProfileAsync(
            CandidateProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Profiles.RemoveAll(profile => profile.Id == profileId);
            return Task.CompletedTask;
        }

        public Task AddFactAsync(
            CandidateFact fact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Facts.Add(fact);
            return Task.CompletedTask;
        }

        public Task UpdateFactAsync(
            CandidateFact fact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task DeleteFactAsync(
            Guid factId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Facts.RemoveAll(fact => fact.Id == factId);
            return Task.CompletedTask;
        }

        private async Task AddProfileAfterReleaseAsync(
            CandidateProfile profile,
            CancellationToken cancellationToken)
        {
            await ReleaseProfileAdd.Task;
            cancellationToken.ThrowIfCancellationRequested();
            Profiles.Add(profile);
        }
    }

    private sealed class FixedClock(DateTime value) : IClock
    {
        public DateTime UtcNow { get; } = value;
    }
}
