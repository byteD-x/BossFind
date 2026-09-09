using BossFind.Application.Profiles;
using BossFind.App.ViewModels;

namespace BossFind.Ui.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task ExportAsync_reports_failure_and_resets_busy_state()
    {
        var exportService = new StubExportService
        {
            Exception = new InvalidOperationException("目标目录不可写")
        };
        var viewModel = new SettingsViewModel(exportService)
        {
            ExportPath = Path.Combine(Path.GetTempPath(), "bossfind-export-test.json")
        };

        await viewModel.ExportAsync();

        Assert.Equal("数据导出失败：目标目录不可写", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(viewModel.ExportPath, exportService.LastPath);
    }

    [Fact]
    public async Task ExportAsync_reports_cancellation_and_resets_busy_state()
    {
        var exportService = new StubExportService
        {
            Exception = new OperationCanceledException()
        };
        var viewModel = new SettingsViewModel(exportService);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await viewModel.ExportAsync(cancellation.Token);

        Assert.Equal("导出已取消。", viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ExportAsync_reports_success_and_resets_busy_state()
    {
        var exportService = new StubExportService();
        var exportPath = Path.Combine(Path.GetTempPath(), "bossfind-export-test.json");
        var viewModel = new SettingsViewModel(exportService)
        {
            ExportPath = exportPath
        };

        await viewModel.ExportAsync();

        Assert.Equal($"数据已导出：{Path.GetFullPath(exportPath)}", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(exportPath, exportService.LastPath);
    }

    private sealed class StubExportService : ICandidateProfileExportService
    {
        public Exception? Exception { get; init; }

        public string? LastPath { get; private set; }

        public Task ExportAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            LastPath = filePath;
            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            return Task.CompletedTask;
        }
    }
}
