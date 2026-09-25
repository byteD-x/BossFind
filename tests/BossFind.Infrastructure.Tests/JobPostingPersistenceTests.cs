using BossFind.Application.Jobs;
using BossFind.Domain.Entities;
using BossFind.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BossFind.Infrastructure.Tests;

public sealed class JobPostingPersistenceTests
{
    [Fact]
    public async Task Recording_the_same_posting_twice_updates_view_count_without_duplicating()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);
            var draft = CreateDraft();

            var first = await service.RecordViewedAsync(draft, draft.Url);
            var second = await service.RecordViewedAsync(draft, draft.Url);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(2, second.ViewCount);
            Assert.Equal("C#、.NET", second.Skills);
            Assert.Equal("五险一金、弹性工作", second.Benefits);

            await using var verification = new AppDbContext(options);
            var stored = Assert.Single(await verification.JobPostings.AsNoTracking().ToListAsync());
            Assert.Equal(2, stored.ViewCount);
            Assert.Equal("后端工程师", stored.Title);
            Assert.Equal("Boss直聘", stored.Platform);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Recording_a_batch_deduplicates_search_cards_and_skips_invalid_links()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var service = new JobPostingService(new CandidateProfileRepository(contexts));

            var saved = await service.RecordViewedBatchAsync(
            [
                CreateDraft(),
                CreateDraft() with { Title = "重复卡片标题" },
                CreateDraft() with { ExternalId = "", Url = "not-a-url" }
            ]);

            var posting = Assert.Single(saved);
            Assert.Equal("后端工程师", posting.Title);
            Assert.Equal(1, posting.ViewCount);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Recording_a_posting_rejects_an_invalid_url()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var service = new JobPostingService(new CandidateProfileRepository(contexts));

            await Assert.ThrowsAsync<ArgumentException>(() => service.RecordViewedAsync(
                CreateDraft() with { Url = "not-a-url" },
                "also-not-a-url"));
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Favorite_survives_later_views_of_the_same_posting()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);
            var draft = CreateDraft();

            var posting = await service.RecordViewedAsync(draft, draft.Url);
            await service.SetFavoriteAsync(posting, true);

            // 再次浏览同一岗位：需要保留收藏状态，且不产生重复记录。
            var revisited = await service.RecordViewedAsync(draft, draft.Url);

            Assert.True(revisited.IsFavorite);
            var favorites = await repository.ListAsync(favoritesOnly: true);
            var favorite = Assert.Single(favorites);
            Assert.Equal(posting.Id, favorite.Id);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Creating_an_application_from_a_revisited_posting_does_not_reinsert_the_posting()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);
            var draft = CreateDraft();

            // RecordViewedAsync 返回游离实体（其 DbContext 已释放）。
            var first = await service.RecordViewedAsync(draft, draft.Url);
            var second = await service.RecordViewedAsync(draft, draft.Url);
            Assert.Equal(first.Id, second.Id);

            var application = await service.CreateApplicationAsync(second, null, "  等待用户在平台页面确认提交  ");

            Assert.Equal(second.Id, application.JobPostingId);
            Assert.Equal(JobApplicationStatus.ReadyForReview, application.Status);
            Assert.Equal("等待用户在平台页面确认提交", application.Note);

            await using var verification = new AppDbContext(options);
            Assert.Equal(1, await verification.JobPostings.CountAsync());
            var stored = Assert.Single(await verification.JobApplications.AsNoTracking().ToListAsync());
            Assert.Equal(second.Id, stored.JobPostingId);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Queuing_a_revisited_posting_does_not_reinsert_the_posting()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);
            var draft = CreateDraft();

            var first = await service.RecordViewedAsync(draft, draft.Url);
            var second = await service.RecordViewedAsync(draft, draft.Url);
            Assert.Equal(first.Id, second.Id);

            var task = await service.QueueAsync(second, null);

            Assert.Equal(second.Id, task.JobPostingId);
            Assert.Equal(JobAutomationTaskStatus.Queued, task.Status);
            Assert.Empty(task.ErrorMessage);

            await using var verification = new AppDbContext(options);
            Assert.Equal(1, await verification.JobPostings.CountAsync());
            var stored = Assert.Single(await verification.JobAutomationTasks.AsNoTracking().ToListAsync());
            Assert.Equal(second.Id, stored.JobPostingId);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Listings_load_job_postings_and_filter_favorites()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);

            var favorite = await service.RecordViewedAsync(CreateDraft(), CreateDraft().Url);
            var other = await service.RecordViewedAsync(
                CreateDraft() with { Title = "数据分析师", ExternalId = "job-2" },
                "https://www.zhipin.com/job_detail/job-2.html");
            await service.SetFavoriteAsync(favorite, true);

            var all = await repository.ListAsync();
            Assert.Equal(2, all.Count);

            var favorites = await repository.ListAsync(favoritesOnly: true);
            var only = Assert.Single(favorites);
            Assert.Equal(favorite.Id, only.Id);

            await service.CreateApplicationAsync(favorite, null, "备注");
            await service.QueueAsync(other, null);

            var applications = await repository.ListApplicationsAsync();
            var application = Assert.Single(applications);
            Assert.Equal("后端工程师", application.JobPosting.Title);

            var tasks = await repository.ListAutomationTasksAsync();
            var queued = Assert.Single(tasks);
            Assert.Equal("数据分析师", queued.JobPosting.Title);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Repeated_queue_requests_reuse_active_task_and_status_changes_are_persisted()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var repository = new CandidateProfileRepository(contexts);
            var service = new JobPostingService(repository);
            var draft = CreateDraft();
            var posting = await service.RecordViewedAsync(draft, draft.Url);

            var firstTask = await service.QueueAsync(posting, null);
            var repeatedTask = await service.QueueAsync(posting, null);
            var application = await service.CreateApplicationAsync(posting, null, "");
            await service.UpdateAutomationTaskStatusAsync(firstTask.Id, JobAutomationTaskStatus.InReview);
            await service.UpdateApplicationStatusAsync(application.Id, JobApplicationStatus.Submitted);

            Assert.Equal(firstTask.Id, repeatedTask.Id);
            var storedTask = Assert.Single(await repository.ListAutomationTasksAsync());
            var storedApplication = Assert.Single(await repository.ListApplicationsAsync());
            Assert.Equal(JobAutomationTaskStatus.InReview, storedTask.Status);
            Assert.Null(storedTask.CompletedAtUtc);
            Assert.Equal(JobApplicationStatus.Submitted, storedApplication.Status);
            Assert.NotNull(storedApplication.SubmittedAtUtc);

            await service.UpdateAutomationTaskStatusAsync(firstTask.Id, JobAutomationTaskStatus.Completed);
            storedTask = Assert.Single(await repository.ListAutomationTasksAsync());
            Assert.Equal(JobAutomationTaskStatus.Completed, storedTask.Status);
            Assert.NotNull(storedTask.CompletedAtUtc);
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    [Fact]
    public async Task Application_creation_rejects_a_null_posting()
    {
        var databasePath = CreateDatabasePath();
        try
        {
            var options = CreateOptions(databasePath);
            await MigrateAsync(options);
            await using var contexts = CreateFactory(databasePath);
            var service = new JobPostingService(new CandidateProfileRepository(contexts));

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.CreateApplicationAsync(null!, null, null));
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => service.QueueAsync(null!, null));
        }
        finally
        {
            DeleteDatabaseFiles(databasePath);
        }
    }

    private static JobPostingDraft CreateDraft()
    {
        return new JobPostingDraft(
            "后端工程师",
            "示例科技",
            "上海",
            ["C#", ".NET"],
            Salary: "20-30K",
            Experience: "3-5年",
            Education: "本科",
            Benefits: ["五险一金", "弹性工作"],
            Description: "负责后端服务开发。",
            ExternalId: "job-1",
            Url: "https://www.zhipin.com/job_detail/job-1.html");
    }

    private static string CreateDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"bossfind-jobs-{Guid.NewGuid():N}.db");
    }

    private static string CreateConnectionString(string databasePath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
    }

    private static DbContextOptions<AppDbContext> CreateOptions(string databasePath)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(CreateConnectionString(databasePath))
            .Options;
    }

    private static TestDbContextFactory CreateFactory(string databasePath)
    {
        var provider = new ServiceCollection()
            .AddDbContextFactory<AppDbContext>(builder => builder.UseSqlite(CreateConnectionString(databasePath)))
            .BuildServiceProvider();
        return new TestDbContextFactory(provider);
    }

    private sealed class TestDbContextFactory(ServiceProvider provider)
        : IDbContextFactory<AppDbContext>, IAsyncDisposable
    {
        private readonly IDbContextFactory<AppDbContext> factory =
            provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        public AppDbContext CreateDbContext()
        {
            return factory.CreateDbContext();
        }

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return factory.CreateDbContextAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }

    private static async Task MigrateAsync(DbContextOptions<AppDbContext> options)
    {
        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static void DeleteDatabaseFiles(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
