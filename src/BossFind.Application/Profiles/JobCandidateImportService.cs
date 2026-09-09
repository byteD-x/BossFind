using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Profiles;

public sealed record JobCandidateImportRequest(
    string CandidateName,
    string Title,
    string? Company,
    string? City,
    IReadOnlyList<string>? Skills);

public sealed class JobCandidateImportService(CandidateProfileService profileService)
{
    private const string SourceText = "Boss 岗位摘要";

    public async Task<CandidateProfile> ImportAsync(
        JobCandidateImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CandidateName);
        ArgumentNullException.ThrowIfNull(request.Title);

        var profile = await profileService.CreateAsync(
            request.CandidateName,
            request.Title,
            request.City,
            cancellationToken: cancellationToken);

        var company = NormalizeOptional(request.Company);
        var city = NormalizeOptional(request.City);
        await AddFactIfPresentAsync(
            profile,
            CandidateFactCategory.Experience,
            company.Length == 0 ? string.Empty : $"公司：{company}",
            cancellationToken);
        await AddFactIfPresentAsync(
            profile,
            CandidateFactCategory.Experience,
            city.Length == 0 ? string.Empty : $"地点：{city}",
            cancellationToken);

        var skills = request.Skills ?? [];
        foreach (var skill in skills
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await AddFactIfPresentAsync(
                profile,
                CandidateFactCategory.Skill,
                skill,
                cancellationToken);
        }

        return profile;
    }

    private async Task AddFactIfPresentAsync(
        CandidateProfile profile,
        CandidateFactCategory category,
        string content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var fact = await profileService.AddFactAsync(
            profile.Id,
            category,
            content,
            SourceText,
            confidence: 1,
            cancellationToken: cancellationToken);
        profile.Facts.Add(fact);
    }

    private static string NormalizeOptional(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
