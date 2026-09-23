using BossFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<CandidateFact> CandidateFacts => Set<CandidateFact>();

    public DbSet<JobPosting> JobPostings => Set<JobPosting>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<JobAutomationTask> JobAutomationTasks => Set<JobAutomationTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
            entity.Property(profile => profile.Headline).HasMaxLength(500);
            entity.Property(profile => profile.Email).HasMaxLength(320);
            entity.Property(profile => profile.Phone).HasMaxLength(50);
            entity.Property(profile => profile.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<CandidateFact>(entity =>
        {
            entity.HasKey(fact => fact.Id);
            entity.Property(fact => fact.Content).HasMaxLength(4000).IsRequired();
            entity.Property(fact => fact.SourceText).HasMaxLength(8000);
            entity.Property(fact => fact.Confidence).HasPrecision(4, 3);
            entity.Property(fact => fact.Version).IsConcurrencyToken();
            entity.HasIndex(fact => new { fact.ProfileId, fact.IsConfirmed });
            entity.HasOne(fact => fact.Profile)
                .WithMany(profile => profile.Facts)
                .HasForeignKey(fact => fact.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.HasKey(posting => posting.Id);
            entity.Property(posting => posting.Platform).HasMaxLength(100).IsRequired();
            entity.Property(posting => posting.ExternalId).HasMaxLength(300);
            entity.Property(posting => posting.Url).HasMaxLength(2000);
            entity.Property(posting => posting.Title).HasMaxLength(500).IsRequired();
            entity.Property(posting => posting.Company).HasMaxLength(500);
            entity.Property(posting => posting.City).HasMaxLength(200);
            entity.Property(posting => posting.Salary).HasMaxLength(200);
            entity.Property(posting => posting.Experience).HasMaxLength(200);
            entity.Property(posting => posting.Education).HasMaxLength(200);
            entity.Property(posting => posting.Benefits).HasMaxLength(4000);
            entity.Property(posting => posting.Description).HasMaxLength(20000);
            entity.Property(posting => posting.Skills).HasMaxLength(4000);
            entity.HasIndex(posting => new { posting.Platform, posting.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(application => application.Id);
            entity.Property(application => application.Note).HasMaxLength(4000);
            entity.HasOne(application => application.JobPosting)
                .WithMany()
                .HasForeignKey(application => application.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(application => application.CandidateProfile)
                .WithMany()
                .HasForeignKey(application => application.CandidateProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JobAutomationTask>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.ErrorMessage).HasMaxLength(4000);
            entity.HasOne(task => task.JobPosting)
                .WithMany()
                .HasForeignKey(task => task.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(task => new { task.Status, task.CreatedAtUtc });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditValues();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditValues()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is CandidateProfile profile)
                {
                    profile.CreatedAtUtc = profile.CreatedAtUtc == default ? now : profile.CreatedAtUtc.ToUniversalTime();
                    profile.UpdatedAtUtc = profile.UpdatedAtUtc == default ? profile.CreatedAtUtc : profile.UpdatedAtUtc.ToUniversalTime();
                    profile.Version = profile.Version == 0 ? 1 : profile.Version;
                }
                else if (entry.Entity is CandidateFact fact)
                {
                    fact.CreatedAtUtc = fact.CreatedAtUtc == default ? now : fact.CreatedAtUtc.ToUniversalTime();
                    fact.UpdatedAtUtc = fact.UpdatedAtUtc == default ? fact.CreatedAtUtc : fact.UpdatedAtUtc.ToUniversalTime();
                    fact.Version = fact.Version == 0 ? 1 : fact.Version;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is CandidateProfile profile)
                {
                    profile.UpdatedAtUtc = now;
                    profile.Version = entry.Property(nameof(CandidateProfile.Version)).OriginalValue is long originalVersion
                        ? originalVersion + 1
                        : profile.Version + 1;
                }
                else if (entry.Entity is CandidateFact fact)
                {
                    fact.UpdatedAtUtc = now;
                    fact.Version = entry.Property(nameof(CandidateFact.Version)).OriginalValue is long originalVersion
                        ? originalVersion + 1
                        : fact.Version + 1;
                }
            }
        }
    }
}
