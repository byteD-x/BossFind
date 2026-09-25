namespace BossFind.Application.Common;

public interface ILocalDataArchiveService
{
    Task BackupAsync(string filePath, CancellationToken cancellationToken = default);

    Task RestoreAsync(string filePath, CancellationToken cancellationToken = default);

    Task ExportApplicationsExcelAsync(string filePath, CancellationToken cancellationToken = default);
}
