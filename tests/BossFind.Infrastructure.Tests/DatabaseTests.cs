using BossFind.Domain.Entities;
using BossFind.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public async Task Migrate_creates_schema_and_persists_profile()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();
                context.CandidateProfiles.Add(new CandidateProfile { Name = "测试候选人" });
                await context.SaveChangesAsync();
            }

            await using (var verificationContext = new AppDbContext(options))
            {
                Assert.Equal("测试候选人", await verificationContext.CandidateProfiles.Select(profile => profile.Name).SingleAsync());
                Assert.Equal(1, await verificationContext.CandidateProfiles.Select(profile => profile.Version).SingleAsync());
            }
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Profile_search_matches_name_headline_and_location_and_ignores_whitespace_query()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                seed.CandidateProfiles.AddRange(
                    new CandidateProfile
                    {
                        Name = "姓名命中候选人",
                        Headline = "普通标题",
                        Location = "普通地点"
                    },
                    new CandidateProfile
                    {
                        Name = "普通候选人",
                        Headline = "标题命中工程师",
                        Location = "普通地点"
                    },
                    new CandidateProfile
                    {
                        Name = "另一位候选人",
                        Headline = "普通标题",
                        Location = "地点命中城市"
                    },
                    new CandidateProfile
                    {
                        Name = "未命中候选人",
                        Headline = "普通标题",
                        Location = "普通地点"
                    });
                await seed.SaveChangesAsync();
            }

            var repository = CreateRepository(options);

            Assert.Equal("姓名命中候选人", Assert.Single(await repository.ListProfilesAsync("姓名命中")).Name);
            Assert.Equal("普通候选人", Assert.Single(await repository.ListProfilesAsync("标题命中")).Name);
            Assert.Equal("另一位候选人", Assert.Single(await repository.ListProfilesAsync("地点命中")).Name);

            var allProfiles = await repository.ListProfilesAsync("   ");
            Assert.Equal(4, allProfiles.Count);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Profile_search_sorts_by_updated_time_then_name()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            var latest = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
            var earlier = latest.AddMinutes(-1);
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                seed.CandidateProfiles.AddRange(
                    new CandidateProfile
                    {
                        Name = "Zeta",
                        CreatedAtUtc = earlier,
                        UpdatedAtUtc = earlier
                    },
                    new CandidateProfile
                    {
                        Name = "Alpha",
                        CreatedAtUtc = latest,
                        UpdatedAtUtc = latest
                    },
                    new CandidateProfile
                    {
                        Name = "Beta",
                        CreatedAtUtc = latest,
                        UpdatedAtUtc = latest
                    },
                    new CandidateProfile
                    {
                        Name = "Gamma",
                        CreatedAtUtc = earlier,
                        UpdatedAtUtc = earlier
                    });
                await seed.SaveChangesAsync();
            }

            var repository = CreateRepository(options);
            var profiles = await repository.ListProfilesAsync(null);

            Assert.Equal(["Alpha", "Beta", "Gamma", "Zeta"], profiles.Select(profile => profile.Name));
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Stale_profile_update_throws_concurrency_exception()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            Guid profileId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                var profile = new CandidateProfile { Name = "初始姓名" };
                seed.Add(profile);
                await seed.SaveChangesAsync();
                profileId = profile.Id;
            }

            await using (var first = new AppDbContext(options))
            await using (var second = new AppDbContext(options))
            {
                var firstProfile = await first.CandidateProfiles.SingleAsync(profile => profile.Id == profileId);
                var secondProfile = await second.CandidateProfiles.SingleAsync(profile => profile.Id == profileId);
                firstProfile.Name = "第一次修改";
                await first.SaveChangesAsync();
                secondProfile.Name = "过期修改";

                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Candidate_fact_requires_an_existing_profile()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            await using var context = new AppDbContext(options);
            await context.Database.MigrateAsync();
            context.CandidateFacts.Add(new CandidateFact
            {
                ProfileId = Guid.NewGuid(),
                Content = "孤儿事实"
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Candidate_fact_is_related_to_profile_and_cascade_deleted()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            Guid profileId;
            Guid factId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                var profile = new CandidateProfile { Name = "有关联的候选人" };
                var fact = new CandidateFact
                {
                    Content = "有关系的事实"
                };
                profile.Facts.Add(fact);
                seed.CandidateProfiles.Add(profile);
                await seed.SaveChangesAsync();
                profileId = profile.Id;
                factId = fact.Id;
            }

            await using (var verification = new AppDbContext(options))
            {
                var fact = await verification.CandidateFacts
                    .Include(candidateFact => candidateFact.Profile)
                    .SingleAsync(candidateFact => candidateFact.Id == factId);
                Assert.Equal(profileId, fact.ProfileId);
                Assert.Equal("有关联的候选人", fact.Profile.Name);

                verification.CandidateProfiles.Remove(fact.Profile);
                await verification.SaveChangesAsync();
            }

            await using (var finalVerification = new AppDbContext(options))
            {
                Assert.False(await finalVerification.CandidateFacts.AnyAsync(candidateFact => candidateFact.Id == factId));
            }
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Stale_fact_update_throws_concurrency_exception()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-{Guid.NewGuid():N}.db");
        try
        {
            var options = CreateOptions(databasePath);
            Guid factId;
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                var profile = new CandidateProfile { Name = "事实并发候选人" };
                var fact = new CandidateFact
                {
                    Content = "初始事实"
                };
                profile.Facts.Add(fact);
                seed.CandidateProfiles.Add(profile);
                await seed.SaveChangesAsync();
                factId = fact.Id;
            }

            await using (var first = new AppDbContext(options))
            await using (var second = new AppDbContext(options))
            {
                var firstFact = await first.CandidateFacts.SingleAsync(fact => fact.Id == factId);
                var secondFact = await second.CandidateFacts.SingleAsync(fact => fact.Id == factId);
                firstFact.Content = "第一次事实修改";
                await first.SaveChangesAsync();
                secondFact.Content = "过期事实修改";

                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static DbContextOptions<AppDbContext> CreateOptions(string databasePath)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Pooling = false
            }.ToString())
            .Options;
    }

    private static CandidateProfileRepository CreateRepository(DbContextOptions<AppDbContext> options)
    {
        return new CandidateProfileRepository(new TestDbContextFactory(options));
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(options);
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AppDbContext(options));
        }
    }

    private static void DeleteDatabaseFiles(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
