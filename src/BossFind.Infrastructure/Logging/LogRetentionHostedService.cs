using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BossFind.Infrastructure.Logging;

/// <summary>
/// 低频清理日志文件，避免历史日志长期占用用户磁盘。
/// </summary>
public sealed partial class LogRetentionHostedService(
    string logDirectory,
    ILogger<LogRetentionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(14);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            await CleanupAsync(stoppingToken);

            using var timer = new PeriodicTimer(CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 应用退出时结束后台任务，不写入无意义的取消日志。
        }
    }

    private Task CleanupAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(logDirectory))
        {
            return Task.CompletedTask;
        }

        var cutoff = DateTime.UtcNow - RetentionPeriod;
        var deletedCount = 0;
        var failedCount = 0;

        try
        {
            foreach (var path in Directory.EnumerateFiles(logDirectory, "bossfind-*.log", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                DateTime lastWriteTimeUtc;
                try
                {
                    lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
                }
                catch (IOException)
                {
                    failedCount++;
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    failedCount++;
                    continue;
                }

                if (lastWriteTimeUtc >= cutoff)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    deletedCount++;
                }
                catch (IOException)
                {
                    failedCount++;
                }
                catch (UnauthorizedAccessException)
                {
                    failedCount++;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            return Task.CompletedTask;
        }
        catch (IOException exception)
        {
            DirectoryReadFailed(exception);
            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException exception)
        {
            DirectoryAccessFailed(exception);
            return Task.CompletedTask;
        }

        if (failedCount > 0)
        {
            CleanupCompletedWithFailures(deletedCount, failedCount);
        }
        else if (deletedCount > 0)
        {
            CleanupCompleted(deletedCount);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Warning, "读取日志目录失败")]
    private partial void DirectoryReadFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "访问日志目录失败")]
    private partial void DirectoryAccessFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "日志清理完成：删除 {DeletedCount} 个文件，{FailedCount} 个文件无法删除")]
    private partial void CleanupCompletedWithFailures(int deletedCount, int failedCount);

    [LoggerMessage(LogLevel.Information, "日志清理完成：删除 {DeletedCount} 个过期文件")]
    private partial void CleanupCompleted(int deletedCount);
}
