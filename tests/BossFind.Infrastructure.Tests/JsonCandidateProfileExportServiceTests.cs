using System.Text;
using System.Text.Json;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;
using BossFind.Infrastructure.Export;
using BossFind.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BossFind.Infrastructure.Tests;

public sealed class JsonCandidateProfileExportServiceTests
{
    [Fact]
    public async Task ExportAsync_writes_profiles_and_facts_as_stable_utf8_json()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"bossfind-export-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(rootDirectory, "database", "bossfind.db");
        var firstExportPath = Path.Combine(rootDirectory, "exports", "first", "profiles.json");
        var secondExportPath = Path.Combine(rootDirectory, "exports", "second", "profiles.json");

        try
        {
            var options = CreateOptions(databasePath);
            await using (var seed = new AppDbContext(options))
            {
                await seed.Database.MigrateAsync();
                var laterProfile = new CandidateProfile
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Name = "林晓",
                    Headline = "C# 工程师",
                    Location = "上海",
                    Email = "lin@example.com",
                    CreatedAtUtc = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc)
                };
                laterProfile.Facts.Add(new CandidateFact
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                    Category = CandidateFactCategory.Skill,
                    Content = "熟悉 C#",
                    SourceText = "简历第 2 页",
                    Confidence = 0.85m,
                    IsConfirmed = true
                });
                laterProfile.Facts.Add(new CandidateFact
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                    Category = CandidateFactCategory.Experience,
                    Content = "后端开发",
                    Confidence = 0.7m
                });

                var earlierProfile = new CandidateProfile
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Name = "赵敏",
                    Headline = "产品经理",
                    CreatedAtUtc = new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc)
                };

                seed.CandidateProfiles.AddRange(earlierProfile, laterProfile);
                await seed.SaveChangesAsync();
            }

            var exporter = new JsonCandidateProfileExportService(CreateRepository(options));
            await exporter.ExportAsync(firstExportPath);
            await exporter.ExportAsync(secondExportPath);

            var firstBytes = await File.ReadAllBytesAsync(firstExportPath);
            var secondBytes = await File.ReadAllBytesAsync(secondExportPath);
            Assert.Equal(firstBytes, secondBytes);
            Assert.NotEmpty(firstBytes);
            Assert.NotEqual(0xEF, firstBytes[0]);

            var json = Encoding.UTF8.GetString(firstBytes);
            Assert.Contains("林晓", json);
            Assert.Contains("熟悉 C#", json);
            Assert.DoesNotContain("\\u6797\\u6653", json, StringComparison.OrdinalIgnoreCase);

            using var document = JsonDocument.Parse(json);
            var profiles = document.RootElement.GetProperty("profiles");
            Assert.Equal(2, profiles.GetArrayLength());
            Assert.Equal("林晓", profiles[0].GetProperty("name").GetString());
            Assert.Equal("赵敏", profiles[1].GetProperty("name").GetString());

            var facts = profiles[0].GetProperty("facts");
            Assert.Equal(2, facts.GetArrayLength());
            Assert.Equal("Experience", facts[0].GetProperty("category").GetString());
            Assert.Equal("Skill", facts[1].GetProperty("category").GetString());
            Assert.Equal("简历第 2 页", facts[1].GetProperty("sourceText").GetString());
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExportAsync_rejects_empty_path(string path)
    {
        var exporter = new JsonCandidateProfileExportService(CreateRepository(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options));

        await Assert.ThrowsAsync<ArgumentException>(() => exporter.ExportAsync(path));
    }

    [Fact]
    public async Task ExportAsync_rejects_null_path()
    {
        var exporter = new JsonCandidateProfileExportService(CreateRepository(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options));

        await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.ExportAsync(null!));
    }

    private static DbContextOptions<AppDbContext> CreateOptions(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
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

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
