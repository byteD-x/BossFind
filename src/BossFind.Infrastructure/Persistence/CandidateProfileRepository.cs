using BossFind.Application.Jobs;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Persistence;

public sealed class CandidateProfileRepository(IDbContextFactory<AppDbContext> contextFactory)
    : ICandidateProfileRepository, IJobRepository
{
    public async Task<IReadOnlyList<CandidateProfile>> ListProfilesAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<CandidateProfile> query = context.CandidateProfiles.AsNoTracking().Include(profile => profile.Facts);
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalizedSearchText = searchText.Trim();
            query = query.Where(profile => profile.Name.Contains(normalizedSearchText) || profile.Headline.Contains(normalizedSearchText) || profile.Location.Contains(normalizedSearchText));
        }

        return await query.OrderByDescending(profile => profile.UpdatedAtUtc).ThenBy(profile => profile.Name).ToListAsync(cancellationToken);
    }

    public async Task<CandidateProfile?> GetProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CandidateProfiles.AsNoTracking().Include(profile => profile.Facts).SingleOrDefaultAsync(profile => profile.Id == profileId, cancellationToken);
    }

    public async Task AddProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProfileAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
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

    public async Task DeleteProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.CandidateProfiles.FindAsync([profileId], cancellationToken);
        if (profile is null) return;
        context.CandidateProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddFactAsync(CandidateFact fact, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CandidateFacts.Add(fact);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFactAsync(CandidateFact fact, CancellationToken cancellationToken = default)
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

    public async Task DeleteFactAsync(Guid factId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var fact = await context.CandidateFacts.FindAsync([factId], cancellationToken);
        if (fact is null) return;
        context.CandidateFacts.Remove(fact);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobPosting?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JobPostings.AsNoTracking().SingleOrDefaultAsync(posting => posting.Id == id, cancellationToken);
    }

    public async Task<JobPosting?> FindByExternalIdAsync(string platform, string externalId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(externalId)) return null;
        return await context.JobPostings.SingleOrDefaultAsync(posting => posting.Platform == platform && posting.ExternalId == externalId, cancellationToken);
    }

    public async Task<IReadOnlyList<JobPosting>> ListAsync(bool favoritesOnly = false, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.JobPostings.AsNoTracking();
        if (favoritesOnly) query = query.Where(posting => posting.IsFavorite);
        return await query.OrderByDescending(posting => posting.LastViewedAtUtc).ThenBy(posting => posting.Title).ToListAsync(cancellationToken);
    }

    public async Task<JobPosting> UpsertAsync(JobPosting posting, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // 这里必须使用跟踪查询：SetValues 只有在实体处于跟踪状态时才会把属性标记为已修改，
        // 配合 AsNoTracking 会导致 SaveChanges 写入 0 行且不报错。
        var existing = await context.JobPostings
            .SingleOrDefaultAsync(item => item.Id == posting.Id, cancellationToken);
        if (existing is null)
        {
            context.JobPostings.Add(posting);
            await context.SaveChangesAsync(cancellationToken);
            return posting;
        }

        context.Entry(existing).CurrentValues.SetValues(posting);
        await context.SaveChangesAsync(cancellationToken);
        // 返回持久化后的实例，避免调用方继续持有与数据库不一致的游离副本。
        return existing;
    }

    public async Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var posting = await context.JobPostings.FindAsync([id], cancellationToken);
        if (posting is null) return;
        posting.IsFavorite = isFavorite;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobApplication> AddApplicationAsync(JobApplication application, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.JobApplications.Add(application);
        await context.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<IReadOnlyList<JobApplication>> ListApplicationsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JobApplications.AsNoTracking().Include(application => application.JobPosting).Include(application => application.CandidateProfile).OrderByDescending(application => application.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task UpdateApplicationStatusAsync(Guid id, JobApplicationStatus status, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var application = await context.JobApplications.FindAsync([id], cancellationToken);
        if (application is null) return;
        var now = DateTime.UtcNow;
        application.Status = status;
        application.SubmittedAtUtc = status == JobApplicationStatus.Submitted ? now : null;
        application.UpdatedAtUtc = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<JobAutomationTask> AddAutomationTaskAsync(JobAutomationTask task, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.JobAutomationTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<JobAutomationTask?> FindActiveAutomationTaskAsync(Guid jobPostingId, Guid? candidateProfileId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JobAutomationTasks.AsNoTracking()
            .Where(task => task.JobPostingId == jobPostingId
                && task.CandidateProfileId == candidateProfileId
                && (task.Status == JobAutomationTaskStatus.Queued || task.Status == JobAutomationTaskStatus.InReview))
            .OrderByDescending(task => task.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobAutomationTask>> ListAutomationTasksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JobAutomationTasks.AsNoTracking().Include(task => task.JobPosting).OrderByDescending(task => task.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task UpdateAutomationTaskStatusAsync(Guid id, JobAutomationTaskStatus status, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var task = await context.JobAutomationTasks.FindAsync([id], cancellationToken);
        if (task is null) return;
        task.Status = status;
        task.CompletedAtUtc = status is JobAutomationTaskStatus.Completed or JobAutomationTaskStatus.Cancelled
            ? DateTime.UtcNow
            : null;
        await context.SaveChangesAsync(cancellationToken);
    }
}
