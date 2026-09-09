using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Persistence;

public sealed class CandidateProfileRepository(IDbContextFactory<AppDbContext> contextFactory)
    : ICandidateProfileRepository
{
    public async Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.CandidateProfiles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalizedSearchText = searchText.Trim();
            query = query.Where(profile =>
                profile.Name.Contains(normalizedSearchText)
                || profile.Headline.Contains(normalizedSearchText)
                || profile.Location.Contains(normalizedSearchText));
        }

        return await query
            .OrderByDescending(profile => profile.UpdatedAtUtc)
            .ThenBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidateProfile?> GetProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CandidateProfiles
            .AsNoTracking()
            .Include(profile => profile.Facts)
            .SingleOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
    }

    public async Task AddProfileAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProfileAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateProfiles.Attach(profile);
        var entry = context.Entry(profile);
        entry.Property(candidateProfile => candidateProfile.Name).IsModified = true;
        entry.Property(candidateProfile => candidateProfile.Headline).IsModified = true;
        entry.Property(candidateProfile => candidateProfile.Location).IsModified = true;
        entry.Property(candidateProfile => candidateProfile.Email).IsModified = true;
        entry.Property(candidateProfile => candidateProfile.Phone).IsModified = true;
        entry.Property(candidateProfile => candidateProfile.Version).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.CandidateProfiles.FindAsync([profileId], cancellationToken);
        if (profile is null)
        {
            return;
        }

        context.CandidateProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddFactAsync(
        CandidateFact fact,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateFacts.Add(fact);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFactAsync(
        CandidateFact fact,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateFacts.Attach(fact);
        var entry = context.Entry(fact);
        entry.Property(candidateFact => candidateFact.Category).IsModified = true;
        entry.Property(candidateFact => candidateFact.Content).IsModified = true;
        entry.Property(candidateFact => candidateFact.SourceText).IsModified = true;
        entry.Property(candidateFact => candidateFact.Confidence).IsModified = true;
        entry.Property(candidateFact => candidateFact.IsConfirmed).IsModified = true;
        entry.Property(candidateFact => candidateFact.Version).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteFactAsync(
        Guid factId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var fact = await context.CandidateFacts.FindAsync([factId], cancellationToken);
        if (fact is null)
        {
            return;
        }

        context.CandidateFacts.Remove(fact);
        await context.SaveChangesAsync(cancellationToken);
    }
}
