using BossFind.Domain.Entities;

namespace BossFind.Application.Matching;

public interface IResumeJobMatchService
{
    Task<JobMatchResult> MatchAsync(
        CandidateProfile profile,
        JobRequirement requirement,
        CancellationToken cancellationToken = default);
}

public sealed class LocalResumeJobMatchService : IResumeJobMatchService
{
    public Task<JobMatchResult> MatchAsync(
        CandidateProfile profile,
        JobRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CandidateJobMatcher.Match(profile, requirement));
    }
}
