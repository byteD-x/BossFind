using BossFind.Infrastructure.Export;
using BossFind.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BossFind.Infrastructure.Tests;

public sealed class LocalDataArchiveServiceTests
{
    [Fact]
    public async Task Restore_rejects_a_corrupt_database_without_replacing_the_current_file()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"bossfind-archive-{Guid.NewGuid():N}.db");
        var backupPath = Path.Combine(Path.GetTempPath(), $"bossfind-archive-{Guid.NewGuid():N}.bak");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Pooling = false
                }.ToString())
                .Options;
            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            File.WriteAllText(backupPath, "this is not a sqlite database");
            await using var provider = new ServiceCollection()
                .AddDbContextFactory<AppDbContext>(builder => builder.UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Pooling = false
                }.ToString()))
                .BuildServiceProvider();
            var repository = new CandidateProfileRepository(provider.GetRequiredService<IDbContextFactory<AppDbContext>>());
            using var archive = new LocalDataArchiveService(databasePath, repository);

            await Assert.ThrowsAsync<InvalidDataException>(() => archive.RestoreAsync(backupPath));

            await using var verification = new AppDbContext(options);
            Assert.True(await verification.Database.CanConnectAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists($"{databasePath}-wal");
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists(backupPath);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
