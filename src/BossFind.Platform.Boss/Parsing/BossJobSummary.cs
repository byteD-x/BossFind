namespace BossFind.Platform.Boss.Parsing;

public sealed record BossJobSummary(
    string Title,
    string Company,
    string City,
    IReadOnlyList<string> Tags,
    string Salary = "",
    string Experience = "",
    string Education = "",
    IReadOnlyList<string>? Benefits = null,
    string Description = "",
    string Url = "",
    string ExternalId = "")
{
    public IReadOnlyList<string> BenefitList => Benefits ?? [];
}
