using BossFind.Application.Profiles;
using BossFind.Application.Common;
using BossFind.Platform.Boss.WebView;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class SettingsViewModel(
    ICandidateProfileExportService exportService,
    ILocalDataArchiveService? archiveService = null)
    : ObservableObject
{
    [ObservableProperty]
    public partial string ExportPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BossFind",
        "Exports",
        "candidate-profiles.json");

    [ObservableProperty]
    public partial string BackupPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BossFind",
        "Backups",
        "bossfind-backup.db");

    [ObservableProperty]
    public partial string RestorePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BossFind",
        "Backups",
        "bossfind-backup.db");

    [ObservableProperty]
    public partial string ExcelExportPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "BossFind",
        "bossfind-applications.xlsx");

    [ObservableProperty]
    public partial string Status { get; set; } = "数据导出只写入本地文件。";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public async Task ExportAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await exportService.ExportAsync(ExportPath, cancellationToken);
            Status = $"数据已导出：{Path.GetFullPath(ExportPath)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "导出已取消。";
        }
        catch (Exception exception)
        {
            Status = $"数据导出失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task BackupAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || archiveService is null) return;
        IsBusy = true;
        try
        {
            await archiveService.BackupAsync(BackupPath, cancellationToken);
            Status = $"本地数据已备份：{Path.GetFullPath(BackupPath)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "备份已取消。";
        }
        catch (Exception exception)
        {
            Status = $"数据备份失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || archiveService is null) return;
        IsBusy = true;
        try
        {
            await archiveService.RestoreAsync(RestorePath, cancellationToken);
            Status = "数据已恢复。请重启应用以刷新当前页面。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "恢复已取消。";
        }
        catch (Exception exception)
        {
            Status = $"数据恢复失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportApplicationsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || archiveService is null) return;
        IsBusy = true;
        try
        {
            await archiveService.ExportApplicationsExcelAsync(ExcelExportPath, cancellationToken);
            Status = $"投递 Excel 报表已导出：{Path.GetFullPath(ExcelExportPath)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "投递报表导出已取消。";
        }
        catch (Exception exception)
        {
            Status = $"投递报表导出失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearWebViewProfile()
    {
        if (IsBusy)
        {
            return;
        }

        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        try
        {
            var cleared = BossWebViewProfileCleaner.Clear(localApplicationData);
            Status = cleared
                ? "WebView2 独立 Profile 已清理。下次打开 Boss 浏览器时会重新创建。"
                : "没有找到可清理的 WebView2 独立 Profile。";
        }
        catch (Exception exception)
        {
            Status = $"WebView2 Profile 清理失败：{exception.Message}";
        }
    }
}
