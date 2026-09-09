using BossFind.Application.Common;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Tests;

public sealed class JobCandidateImportServiceTests
{
    [Fact]
    public async Task ImportAsync_creates_profile_and_maps_job_summary_to_facts()
    {
        var repository = new InMemoryRepository();
        var service = new JobCandidateImportService(
            new CandidateProfileService(
                new FixedClock(new DateTime(2026, 9, 8, 1, 0, 0, DateTimeKind.Utc)),
                repository));

        var profile = await service.ImportAsync(
            new JobCandidateImportRequest(
                "  林晓  ",
                "高级 C# 工程师",
                "示例科技",
                "上海",
                ["C#", " .NET ", "C#"]));

        Assert.Equal("林晓", profile.Name);
        Assert.Equal("高级 C# 工程师", profile.Headline);
        Assert.Equal("上海", profile.Location);
        Assert.Same(profile, Assert.Single(repository.Profiles));
        Assert.Equal(
            [CandidateFactCategory.Experience, CandidateFactCategory.Experience, CandidateFactCategory.Skill, CandidateFactCategory.Skill],
            repository.Facts.Select(fact => fact.Category));
        Assert.Contains(repository.Facts, fact => fact.Content == "公司：示例科技");
        Assert.Contains(repository.Facts, fact => fact.Content == "地点：上海");
        Assert.Contains(repository.Facts, fact => fact.Content == "C#");
        Assert.Contains(repository.Facts, fact => fact.Content == ".NET");
    }

    [Fact]
    public async Task ImportAsync_ignores_empty_optional_summary_values()
    {
        var repository = new InMemoryRepository();
        var service = new JobCandidateImportService(
            new CandidateProfileService(new FixedClock(DateTime.UtcNow), repository));

        var profile = await service.ImportAsync(
            new JobCandidateImportRequest("候选人", "", "", "", []));

        Assert.Empty(repository.Facts);
        Assert.Equal(string.Empty, profile.Headline);
    }

    [Fact]
    public async Task ImportAsync_rejects_null_request()
    {
        var service = CreateService(new InMemoryRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ImportAsync(null!));
    }

    [Fact]
    public async Task ImportAsync_rejects_null_candidate_name()
    {
        var service = CreateService(new InMemoryRepository());
        var request = new JobCandidateImportRequest(
            null!,
            "岗位",
            null,
            null,
            null);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ImportAsync(request));
    }

    [Fact]
    public async Task ImportAsync_honors_cancellation_before_persisting_profile()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ImportAsync(
            new JobCandidateImportRequest("候选人", "岗位", null, null, null),
            cancellation.Token));

        Assert.Empty(repository.Profiles);
        Assert.Empty(repository.Facts);
    }

    [Fact]
    public async Task ImportAsync_ignores_empty_and_case_insensitive_duplicate_skills()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);

        await service.ImportAsync(
            new JobCandidateImportRequest(
                "候选人",
                "岗位",
                null,
                null,
                [string.Empty, "  ", "C#", " c# ", ".NET", ".net"]));

        Assert.Equal(
            ["C#", ".NET"],
            repository.Facts
                .Where(fact => fact.Category == CandidateFactCategory.Skill)
                .Select(fact => fact.Content));
    }

    private static JobCandidateImportService CreateService(InMemoryRepository repository)
    {
        return new JobCandidateImportService(
            new CandidateProfileService(
                new FixedClock(new DateTime(2026, 9, 8, 1, 0, 0, DateTimeKind.Utc)),
                repository));
    }

    private sealed class InMemoryRepository : ICandidateProfileRepository
    {
        public List<CandidateProfile> Profiles { get; } = [];

        public List<CandidateFact> Facts { get; } = [];

        public Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(
            string? searchText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CandidateProfile>>(Profiles);
        }

        public Task<CandidateProfile?> GetProfileAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == profileId));
        }

        public Task AddProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            Profiles.RemoveAll(profile => profile.Id == profileId);
            return Task.CompletedTask;
        }

        public Task AddFactAsync(CandidateFact fact, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Facts.Add(fact);
            return Task.CompletedTask;
        }

        public Task UpdateFactAsync(CandidateFact fact, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteFactAsync(Guid factId, CancellationToken cancellationToken = default)
        {
            Facts.RemoveAll(fact => fact.Id == factId);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime value) : IClock
    {
        public DateTime UtcNow { get; } = value;
    }
}
