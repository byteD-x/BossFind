namespace BossFind.Application.Matching;

public sealed record JobMatchResult(
    decimal Score,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    string Explanation);
