using BossFind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<CandidateFact> CandidateFacts => Set<CandidateFact>();

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
