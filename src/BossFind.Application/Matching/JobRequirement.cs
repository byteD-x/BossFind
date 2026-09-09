namespace BossFind.Application.Matching;

public sealed record JobRequirement(
    string Title,
    string? Company,
    string? City,
    IReadOnlyList<string>? RequiredSkills);
