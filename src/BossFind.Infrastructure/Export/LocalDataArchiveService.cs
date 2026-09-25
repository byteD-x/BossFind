using System.Text;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using BossFind.Application.Common;
using BossFind.Application.Jobs;
using Microsoft.Data.Sqlite;

namespace BossFind.Infrastructure.Export;

public sealed class LocalDataArchiveService(
    string databasePath,
    IJobRepository jobRepository) : ILocalDataArchiveService, IDisposable
{
    private readonly SemaphoreSlim archiveGate = new(1, 1);

    private static readonly IReadOnlyList<string> HeaderCells =
        ["公司", "岗位", "岗位JD", "投递状态", "所用简历", "城市", "薪资", "更新时间"];

    public async Task BackupAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await archiveGate.WaitAsync(cancellationToken);
        try
        {
            await CopyDatabaseAsync(filePath, cancellationToken);
        }
        finally
        {
            archiveGate.Release();
        }
    }

    public async Task RestoreAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await archiveGate.WaitAsync(cancellationToken);
        try
        {
            await RestoreDatabaseAsync(filePath, cancellationToken);
        }
        finally
        {
            archiveGate.Release();
        }
    }

    public async Task ExportApplicationsExcelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await archiveGate.WaitAsync(cancellationToken);
        try
        {
            await ExportApplicationsExcelCoreAsync(filePath, cancellationToken);
        }
        finally
        {
            archiveGate.Release();
        }
    }

    private async Task ExportApplicationsExcelCoreAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var applications = await jobRepository.ListApplicationsAsync(cancellationToken);
        var rows = new List<IReadOnlyList<string>>
        {
            HeaderCells
        };
        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new[]
            {
                application.JobPosting.Company,
                application.JobPosting.Title,
                application.JobPosting.Description,
                ToStatusLabel(application.Status),
                application.CandidateProfile?.Name ?? "未关联简历",
                application.JobPosting.City,
                application.JobPosting.Salary,
                application.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            });
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(filePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        AddEntry(archive, "[Content_Types].xml", ContentTypesXml);
        AddEntry(archive, "_rels/.rels", RootRelationshipsXml);
        AddEntry(archive, "xl/workbook.xml", WorkbookXml);
        AddEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
        AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));
    }

    private async Task CopyDatabaseAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("本地数据库不存在。", databasePath);
        }

        var source = Path.GetFullPath(databasePath);
        var target = Path.GetFullPath(filePath);
        EnsureDifferentPath(source, target);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryTarget = CreateTemporaryPath(target);
        try
        {
            await Task.Run(() => BackupDatabase(source, temporaryTarget), cancellationToken);
            await ValidateDatabaseAsync(temporaryTarget, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ReplaceFile(temporaryTarget, target);
        }
        finally
        {
            DeleteIfExists(temporaryTarget);
        }
    }

    private async Task RestoreDatabaseAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = Path.GetFullPath(filePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到备份文件。", sourcePath);
        }

        var targetPath = Path.GetFullPath(databasePath);
        EnsureDifferentPath(sourcePath, targetPath);
        await ValidateDatabaseAsync(sourcePath, cancellationToken);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryTarget = CreateTemporaryPath(targetPath);
        try
        {
            await using (var source = File.OpenRead(sourcePath))
            await using (var destination = File.Create(temporaryTarget))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            ReplaceFile(temporaryTarget, targetPath);
            DeleteIfExists($"{targetPath}-wal");
            DeleteIfExists($"{targetPath}-shm");
        }
        finally
        {
            DeleteIfExists(temporaryTarget);
        }
    }

    private static void BackupDatabase(string sourcePath, string targetPath)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        var targetConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = targetPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        using var source = new SqliteConnection(sourceConnectionString);
        using var target = new SqliteConnection(targetConnectionString);
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static void ReplaceFile(string sourcePath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(sourcePath, targetPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(sourcePath, targetPath);
        }
    }

    private static string CreateTemporaryPath(string targetPath)
    {
        return $"{targetPath}.{Guid.NewGuid():N}.tmp";
    }

    private static void EnsureDifferentPath(string sourcePath, string targetPath)
    {
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("备份文件不能与当前数据库相同。");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (!string.Equals(Convert.ToString(result, CultureInfo.InvariantCulture), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("备份数据库完整性校验失败。");
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("备份文件不是有效的 SQLite 数据库。", exception);
        }
    }

    public void Dispose()
    {
        archiveGate.Dispose();
    }

    private static string BuildWorksheetXml(List<IReadOnlyList<string>> rows)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetData = new XElement(ns + "sheetData");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new XElement(ns + "row", new XAttribute("r", rowIndex + 1));
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex] ?? string.Empty;
                row.Add(new XElement(ns + "c",
                    new XAttribute("r", $"{ColumnName(columnIndex)}{rowIndex + 1}"),
                    new XAttribute("t", "inlineStr"),
                    new XElement(ns + "is", new XElement(ns + "t", value))));
            }

            sheetData.Add(row);
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ns + "worksheet", sheetData)).ToString(SaveOptions.DisableFormatting);
    }

    private static string ColumnName(int index)
    {
        var result = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
        {
            result = (char)('A' + (value - 1) % 26) + result;
        }

        return result;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
        """;

    private const string RootRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
        """;

    private const string WorkbookXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="投递记录" sheetId="1" r:id="rId1"/></sheets></workbook>
        """;

    private const string WorkbookRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
        """;

    private static string ToStatusLabel(Domain.Entities.JobApplicationStatus status) => status switch
    {
        Domain.Entities.JobApplicationStatus.Draft => "草稿",
        Domain.Entities.JobApplicationStatus.ReadyForReview => "待确认",
        Domain.Entities.JobApplicationStatus.Submitted => "已投递",
        Domain.Entities.JobApplicationStatus.Contacting => "沟通中",
        Domain.Entities.JobApplicationStatus.WrittenTest => "待笔试",
        Domain.Entities.JobApplicationStatus.FirstInterview => "一面",
        Domain.Entities.JobApplicationStatus.SecondInterview => "二面",
        Domain.Entities.JobApplicationStatus.FinalInterview => "终面",
        Domain.Entities.JobApplicationStatus.Offer => "Offer",
        Domain.Entities.JobApplicationStatus.Rejected => "已拒绝",
        Domain.Entities.JobApplicationStatus.Withdrawn or Domain.Entities.JobApplicationStatus.Abandoned => "主动放弃",
        _ => "失败"
    };
}
