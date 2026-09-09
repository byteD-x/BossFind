using BossFind.Domain.Entities;

namespace BossFind.Application.Matching;

public sealed class CandidateJobMatcher
{
    private const decimal TitleWeight = 0.4m;
    private const decimal CityWeight = 0.2m;
    private const decimal SkillsWeight = 0.4m;

    public static JobMatchResult Match(
        CandidateProfile candidate,
        JobRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(requirement);
        if (string.IsNullOrWhiteSpace(requirement.Title))
        {
            throw new ArgumentException("岗位标题不能为空。", nameof(requirement));
        }

        var requiredSkills = NormalizeSkills(requirement.RequiredSkills);
        var candidateText = string.Join(
            ' ',
            new[] { candidate.Name, candidate.Headline, candidate.Location }
                .Concat(candidate.Facts.Select(static fact => fact.Content))
                .Where(static value => !string.IsNullOrWhiteSpace(value)));

        var matchedSkills = requiredSkills
            .Where(skill => candidateText.Contains(skill, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var missingSkills = requiredSkills
            .Except(matchedSkills, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var titleMatched = Contains(candidate.Headline, requirement.Title);
        var cityRequired = !string.IsNullOrWhiteSpace(requirement.City);
        var cityMatched = cityRequired && Contains(candidate.Location, requirement.City!);

        var weightedScore = TitleWeight * (titleMatched ? 1m : 0m);
        var activeWeight = TitleWeight;
        if (cityRequired)
        {
            activeWeight += CityWeight;
            weightedScore += CityWeight * (cityMatched ? 1m : 0m);
        }

        if (requiredSkills.Length > 0)
        {
            activeWeight += SkillsWeight;
            weightedScore += SkillsWeight * matchedSkills.Length / requiredSkills.Length;
        }

        var score = activeWeight == 0
            ? 0m
            : Math.Round(Math.Clamp(weightedScore / activeWeight, 0m, 1m), 3);
        var cityStatus = cityRequired
            ? cityMatched ? "城市命中" : "城市未命中"
            : "未设置城市要求";

        return new JobMatchResult(
            score,
            matchedSkills,
            missingSkills,
            $"标题 {(titleMatched ? "命中" : "未命中")}；{cityStatus}；技能命中 {matchedSkills.Length}/{requiredSkills.Length}。" );
    }

    private static string[] NormalizeSkills(IReadOnlyList<string>? skills)
    {
        return (skills ?? [])
            .Select(static skill => skill?.Trim() ?? string.Empty)
            .Where(static skill => skill.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool Contains(string? value, string required)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(required.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
