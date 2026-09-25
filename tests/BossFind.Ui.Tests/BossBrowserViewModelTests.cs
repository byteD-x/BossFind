using BossFind.App.ViewModels;
using BossFind.Application.Common;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

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
        Assert.Equal("已导入求职者简历：林晓", viewModel.Status);
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

        Assert.Equal("已导入求职者简历：待补充求职者简历", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.Single(repository.Profiles);
    }

    [Fact]
    public async Task MatchAsync_sorts_profiles_and_reports_skill_details()
    {
        var (viewModel, repository) = CreateViewModel();
        repository.Profiles.AddRange(
        [
            new CandidateProfile
            {
                Name = "部分匹配",
                Headline = "后端工程师",
                Location = "上海",
                Facts = [new CandidateFact { Content = "熟悉 C#" }]
            },
            new CandidateProfile
            {
                Name = "完全匹配",
                Headline = "后端工程师",
                Location = "上海",
                Facts = [new CandidateFact { Category = CandidateFactCategory.Skill, Content = "C#、.NET" }]
            }
        ]);
        viewModel.SetJobSummary("后端工程师", "示例科技", "上海", ["C#", ".NET"]);

        await viewModel.MatchAsync();

        Assert.Equal(["完全匹配", "部分匹配"], viewModel.Matches.Select(match => match.Profile.Name));
        Assert.Equal("100%", viewModel.Matches[0].ScoreText);
        Assert.Equal("C#、.NET", viewModel.Matches[0].MatchedSkillsText);
        Assert.Equal("无", viewModel.Matches[0].MissingSkillsText);
        Assert.Equal("命中技能：C#、.NET", viewModel.Matches[0].MatchedSkillsLabel);
        Assert.Equal("缺失技能：无", viewModel.Matches[0].MissingSkillsLabel);
        Assert.Equal("已为 2 份简历生成匹配度。", viewModel.MatchStatus);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task MatchAsync_reports_when_no_profiles_exist()
    {
        var (viewModel, _) = CreateViewModel();
        viewModel.SetJobSummary("后端工程师", "示例科技", "上海", []);

        await viewModel.MatchAsync();

        Assert.Empty(viewModel.Matches);
        Assert.Equal("暂无可用简历，请先在求职者简历中创建档案。", viewModel.MatchStatus);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task MatchAsync_reports_cancellation_and_resets_busy_state()
    {
        var (viewModel, repository) = CreateViewModel();
        repository.Profiles.Add(new CandidateProfile { Name = "候选人", Headline = "后端工程师" });
        viewModel.SetJobSummary("后端工程师", "示例科技", "上海", []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.MatchAsync(cancellation.Token);

        Assert.Equal("匹配已取消。", viewModel.MatchStatus);
        Assert.Empty(viewModel.Matches);
        Assert.False(viewModel.IsBusy);
    }

    private static (BossBrowserViewModel ViewModel, InMemoryRepository Repository) CreateViewModel()
    {
        var repository = new InMemoryRepository();
        var profileService = new CandidateProfileService(
            new FixedClock(new DateTime(2026, 9, 8, 1, 0, 0, DateTimeKind.Utc)),
            repository);
        return (
            new BossBrowserViewModel(
                new JobCandidateImportService(profileService),
                profileService),
            repository);
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
