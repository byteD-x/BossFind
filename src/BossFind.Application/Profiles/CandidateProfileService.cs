using BossFind.Application.Common;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Profiles;

public sealed class CandidateProfileService
{
    private readonly IClock clock;
    private readonly ICandidateProfileRepository? repository;

    public CandidateProfileService(IClock clock)
    {
        this.clock = clock;
    }

    public CandidateProfileService(IClock clock, ICandidateProfileRepository repository)
        : this(clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public CandidateProfile Create(string name, string headline)
    {
        ValidateProfile(name, headline);

        var now = clock.UtcNow;

        return new CandidateProfile
        {
            Name = name.Trim(),
            Headline = headline.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public async Task<IReadOnlyList<CandidateProfile>> ListAsync(
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        return await GetRepository().ListProfilesAsync(searchText, cancellationToken);
    }

    public async Task<CandidateProfile?> GetAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        return await GetRepository().GetProfileAsync(profileId, cancellationToken);
    }

    public async Task<CandidateProfile> CreateAsync(
        string name,
        string headline,
        string? location = null,
        string? email = null,
        string? phone = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProfile(name, headline);
        var profile = Create(name, headline);
        profile.Location = NormalizeOptional(location);
        profile.Email = NormalizeOptional(email);
        profile.Phone = NormalizeOptional(phone);
        await GetRepository().AddProfileAsync(profile, cancellationToken);
        return profile;
    }

    public async Task UpdateAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(profile.Id, Guid.Empty);
        ValidateProfile(profile.Name, profile.Headline);
        profile.Name = profile.Name.Trim();
        profile.Headline = profile.Headline.Trim();
        profile.Location = NormalizeOptional(profile.Location);
        profile.Email = NormalizeOptional(profile.Email);
        profile.Phone = NormalizeOptional(profile.Phone);
        await GetRepository().UpdateProfileAsync(profile, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        await GetRepository().DeleteProfileAsync(profileId, cancellationToken);
    }

    public async Task<CandidateFact> AddFactAsync(
        Guid profileId,
        CandidateFactCategory category,
        string content,
        string? sourceText = null,
        decimal confidence = 0,
        bool isConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFact(profileId, content, confidence);
        var now = clock.UtcNow;
        var fact = new CandidateFact
        {
            ProfileId = profileId,
            Category = category,
            Content = content.Trim(),
            SourceText = NormalizeOptional(sourceText),
            Confidence = confidence,
            IsConfirmed = isConfirmed,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await GetRepository().AddFactAsync(fact, cancellationToken);
        return fact;
    }

    public async Task UpdateFactAsync(
        CandidateFact fact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ValidateFact(fact.ProfileId, fact.Content, fact.Confidence);
        fact.Content = fact.Content.Trim();
        fact.SourceText = NormalizeOptional(fact.SourceText);
        await GetRepository().UpdateFactAsync(fact, cancellationToken);
    }

    public async Task DeleteFactAsync(
        Guid factId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(factId, Guid.Empty);
        await GetRepository().DeleteFactAsync(factId, cancellationToken);
    }

    private ICandidateProfileRepository GetRepository()
    {
        return repository ?? throw new InvalidOperationException("当前服务未配置候选人数据存储。");
    }

    private static void ValidateProfile(string name, string headline)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("候选人姓名不能为空。", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(headline);
    }

    private static void ValidateFact(Guid profileId, string content, decimal confidence)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("事实内容不能为空。", nameof(content));
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "置信度必须介于 0 和 1 之间。");
        }
    }

    private static string NormalizeOptional(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
