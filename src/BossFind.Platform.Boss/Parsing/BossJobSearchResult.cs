namespace BossFind.Platform.Boss.Parsing;

public sealed record BossJobSearchResult(
    string Title,
    string Company,
    string City,
    string Salary,
    string Experience,
    string Education,
    IReadOnlyList<string> Tags,
    string Url,
    string ExternalId)
{
    public IReadOnlyList<string> SkillTags => Tags;
}
