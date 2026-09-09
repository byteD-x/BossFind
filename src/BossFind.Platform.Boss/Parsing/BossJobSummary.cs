namespace BossFind.Platform.Boss.Parsing;

public sealed record BossJobSummary(
    string Title,
    string Company,
    string City,
    IReadOnlyList<string> Tags);
