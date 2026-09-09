using BossFind.Application.Common;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Tests;

public sealed class CandidateProfileServiceTests
{
    [Fact]
    public void Create_trims_input_and_uses_clock()
    {
        var now = new DateTime(2026, 9, 6, 12, 30, 0, DateTimeKind.Utc);
        var service = new CandidateProfileService(new FixedClock(now));

        var profile = service.Create("  林晓  ", "  C# 工程师  ");

        Assert.Equal("林晓", profile.Name);
        Assert.Equal("C# 工程师", profile.Headline);
        Assert.Equal(now, profile.CreatedAtUtc);
        Assert.Equal(now, profile.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_name(string? name)
    {
        var service = new CandidateProfileService(new FixedClock(DateTime.UtcNow));

        Assert.Throws<ArgumentException>(() => service.Create(name!, "标题"));
    }

    [Fact]
    public async Task CreateAsync_normalizes_optional_fields_and_persists_profile()
    {
        var repository = new InMemoryRepository();
        var service = new CandidateProfileService(
            new FixedClock(new DateTime(2026, 9, 6, 12, 30, 0, DateTimeKind.Utc)),
            repository);

        var profile = await service.CreateAsync(
            "  林晓  ",
            "  C# 工程师  ",
            " 上海 ",
            " lin@example.com ",
            " 13800000000 ");

        Assert.Equal("林晓", profile.Name);
        Assert.Equal("C# 工程师", profile.Headline);
        Assert.Equal("上海", profile.Location);
        Assert.Equal("lin@example.com", profile.Email);
        Assert.Equal("13800000000", profile.Phone);
        Assert.Same(profile, Assert.Single(repository.Profiles));
    }

    [Fact]
    public async Task AddFactAsync_normalizes_source_and_persists_fact()
    {
        var repository = new InMemoryRepository();
        var service = new CandidateProfileService(
            new FixedClock(DateTime.UtcNow),
            repository);
        var profile = new CandidateProfile();

        var fact = await service.AddFactAsync(
            profile.Id,
            CandidateFactCategory.Skill,
            "  熟悉 C#  ",
            " 简历第 2 页 ",
            0.85m,
            true);

        Assert.Equal("熟悉 C#", fact.Content);
        Assert.Equal("简历第 2 页", fact.SourceText);
        Assert.Equal(0.85m, fact.Confidence);
        Assert.True(fact.IsConfirmed);
        Assert.Same(fact, Assert.Single(repository.Facts));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task AddFactAsync_rejects_confidence_outside_range(double confidence)
    {
        var service = new CandidateProfileService(
            new FixedClock(DateTime.UtcNow),
            new InMemoryRepository());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AddFactAsync(
            Guid.NewGuid(),
            CandidateFactCategory.Skill,
            "事实",
            confidence: (decimal)confidence));
    }

    [Fact]
    public async Task ListAsync_forwards_search_text_to_repository()
    {
        var repository = new InMemoryRepository();
        var service = new CandidateProfileService(new FixedClock(DateTime.UtcNow), repository);

        await service.ListAsync("  上海  ");

        Assert.Equal("  上海  ", repository.LastSearchText);
    }

    private sealed class InMemoryRepository : ICandidateProfileRepository
    {
        public List<CandidateProfile> Profiles { get; } = [];

        public List<CandidateFact> Facts { get; } = [];

        public string? LastSearchText { get; private set; }

        public Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(
            string? searchText,
            CancellationToken cancellationToken = default)
        {
            LastSearchText = searchText;
            IReadOnlyList<CandidateProfile> profiles = Profiles;
            return Task.FromResult(profiles);
        }

        public Task<CandidateProfile?> GetProfileAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == profileId));
        }

        public Task AddProfileAsync(
            CandidateProfile profile,
            CancellationToken cancellationToken = default)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateProfileAsync(
            CandidateProfile profile,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(
            Guid profileId,
            CancellationToken cancellationToken = default)
        {
            Profiles.RemoveAll(profile => profile.Id == profileId);
            return Task.CompletedTask;
        }

        public Task AddFactAsync(
            CandidateFact fact,
            CancellationToken cancellationToken = default)
        {
            Facts.Add(fact);
            return Task.CompletedTask;
        }

        public Task UpdateFactAsync(
            CandidateFact fact,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteFactAsync(
            Guid factId,
            CancellationToken cancellationToken = default)
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
