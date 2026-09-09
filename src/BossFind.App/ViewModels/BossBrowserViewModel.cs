using BossFind.Application.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class BossBrowserViewModel(JobCandidateImportService importService)
    : ObservableObject
{
    [ObservableProperty]
    public partial string CandidateName { get; set; } = "待补充候选人";

    [ObservableProperty]
    public partial string Status { get; set; } = "等待初始化 WebView2";

    [ObservableProperty]
    public partial string ProfilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string JobTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Company { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string City { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSummaryReady { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void SetJobSummary(
        string title,
        string company,
        string city,
        IReadOnlyList<string> tags)
    {
        JobTitle = title;
        Company = company;
        City = city;
        Tags = string.Join("、", tags);
        IsSummaryReady = !string.IsNullOrWhiteSpace(JobTitle);
    }

    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || !IsSummaryReady)
        {
            if (!IsBusy)
            {
                Status = "请先加载有效的岗位摘要。";
            }

            return;
        }

        IsBusy = true;
        try
        {
            var profile = await importService.ImportAsync(
                new JobCandidateImportRequest(
                    CandidateName,
                    JobTitle,
                    Company,
                    City,
                    Tags.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                cancellationToken);
            Status = $"已导入候选人档案：{profile.Name}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "导入已取消。";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
