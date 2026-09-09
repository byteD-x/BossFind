using BossFind.Domain.Entities;

namespace BossFind.Application.Profiles;

public interface ICandidateProfileRepository
{
    Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(
        string? searchText,
        CancellationToken cancellationToken = default);

    Task<CandidateProfile?> GetProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task AddProfileAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken = default);

    Task UpdateProfileAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken = default);

    Task DeleteProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task AddFactAsync(
        CandidateFact fact,
        CancellationToken cancellationToken = default);

    Task UpdateFactAsync(
        CandidateFact fact,
        CancellationToken cancellationToken = default);

    Task DeleteFactAsync(
        Guid factId,
        CancellationToken cancellationToken = default);
}
