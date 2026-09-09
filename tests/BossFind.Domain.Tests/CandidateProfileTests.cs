using BossFind.Domain.Entities;

namespace BossFind.Domain.Tests;

public sealed class CandidateProfileTests
{
    [Fact]
    public void New_profile_has_guid_and_utc_timestamps()
    {
        var profile = new CandidateProfile();

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal(DateTimeKind.Utc, profile.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, profile.UpdatedAtUtc.Kind);
    }

    [Fact]
    public void Fact_version_starts_at_zero_until_persisted()
    {
        var fact = new CandidateFact();

        Assert.Equal(0, fact.Version);
        Assert.False(fact.IsConfirmed);
    }
}
