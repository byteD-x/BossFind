namespace BossFind.Application.Profiles;

public interface ICandidateProfileExportService
{
    Task ExportAsync(string filePath, CancellationToken cancellationToken = default);
}
