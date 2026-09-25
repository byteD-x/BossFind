using System.Collections.ObjectModel;
using System.Globalization;
using BossFind.Application.Jobs;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class ApplicationsViewModel(
    IJobRepository jobRepository,
    JobPostingService jobPostingService) : ObservableObject
{
    public ObservableCollection<ApplicationRow> Applications { get; } = [];

    public ObservableCollection<ApplicationRow> TodoApplications { get; } = [];

    public ObservableCollection<ApplicationRow> SubmittedApplications { get; } = [];

    public ObservableCollection<ApplicationRow> ReadApplications { get; } = [];

    public ObservableCollection<ApplicationRow> InterviewApplications { get; } = [];

    public ObservableCollection<ApplicationRow> OfferApplications { get; } = [];

    public ObservableCollection<ApplicationRow> ClosedApplications { get; } = [];

    public ObservableCollection<ReviewTaskRow> ReviewTasks { get; } = [];

    public ObservableCollection<ReviewTaskRow> QueuedReviewTasks { get; } = [];

    public ObservableCollection<ReviewTaskRow> InReviewTasks { get; } = [];

    public ObservableCollection<ReviewTaskRow> CompletedReviewTasks { get; } = [];

    public IReadOnlyList<string> ApplicationStages { get; } =
    [
        "待确认",
        "沟通中",
        "已投递",
        "待笔试",
        "一面",
        "二面",
        "终面",
        "Offer",
        "已拒绝",
        "主动放弃"
    ];

    [ObservableProperty]
    public partial ApplicationRow? SelectedApplication { get; set; }

    [ObservableProperty]
    public partial ReviewTaskRow? SelectedTask { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string EmptyApplicationsMessage { get; set; } = "";

    [ObservableProperty]
    public partial string EmptyTasksMessage { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedApplicationStage { get; set; } = "";

    public bool CanMarkSubmitted => SelectedApplication?.Application.Status is JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview;

    public bool HasSelectedApplication => SelectedApplication is not null;

    public bool CanWithdraw => SelectedApplication?.Application.Status is JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview;

    public bool CanUpdateStage => SelectedApplication is not null
        && !string.IsNullOrWhiteSpace(SelectedApplicationStage);

    public bool CanStartReview => SelectedTask?.Task.Status is JobAutomationTaskStatus.Queued or JobAutomationTaskStatus.InReview;

    public bool CanCompleteReview => SelectedTask?.Task.Status == JobAutomationTaskStatus.InReview;

    public bool CanCancelTask => SelectedTask?.Task.Status is JobAutomationTaskStatus.Queued or JobAutomationTaskStatus.InReview;

    partial void OnSelectedApplicationChanged(ApplicationRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedApplication));
        OnPropertyChanged(nameof(CanMarkSubmitted));
        OnPropertyChanged(nameof(CanWithdraw));
        SelectedApplicationStage = value is null ? "" : ToStageLabel(value.Application.Status);
        OnPropertyChanged(nameof(CanUpdateStage));
    }

    partial void OnSelectedTaskChanged(ReviewTaskRow? value)
    {
        OnPropertyChanged(nameof(CanStartReview));
        OnPropertyChanged(nameof(CanCompleteReview));
        OnPropertyChanged(nameof(CanCancelTask));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsLoading = true;
        IsBusy = true;
        try
        {
            await ReloadListsAsync(cancellationToken);
            StatusMessage = $"{Applications.Count} 条投递记录，{ReviewTasks.Count} 条复核任务。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "加载已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"投递记录加载失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }
    }

    public Task<bool> MarkSubmittedAsync(CancellationToken cancellationToken = default) =>
        UpdateApplicationAsync(JobApplicationStatus.Submitted, "已标记为已投递。", cancellationToken);

    public Task<bool> WithdrawApplicationAsync(CancellationToken cancellationToken = default) =>
        UpdateApplicationAsync(JobApplicationStatus.Withdrawn, "投递记录已撤销。", cancellationToken);

    public Task<bool> UpdateApplicationStageAsync(CancellationToken cancellationToken = default)
    {
        if (!TryParseStage(SelectedApplicationStage, out var status))
        {
            StatusMessage = "请选择有效的投递阶段。";
            return Task.FromResult(false);
        }

        return UpdateApplicationAsync(status, $"投递阶段已更新为“{SelectedApplicationStage}”。", cancellationToken);
    }

    public Task<bool> BeginReviewAsync(CancellationToken cancellationToken = default) =>
        UpdateTaskAsync(JobAutomationTaskStatus.InReview, "已开始复核。", cancellationToken);

    public Task<bool> CompleteReviewAsync(CancellationToken cancellationToken = default) =>
        UpdateTaskAsync(JobAutomationTaskStatus.Completed, "复核已完成。", cancellationToken);

    public Task<bool> CancelTaskAsync(CancellationToken cancellationToken = default) =>
        UpdateTaskAsync(JobAutomationTaskStatus.Cancelled, "复核任务已取消。", cancellationToken);

    private async Task<bool> UpdateApplicationAsync(
        JobApplicationStatus status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (IsBusy || SelectedApplication is null) return false;
        IsBusy = true;
        try
        {
            await jobPostingService.UpdateApplicationStatusAsync(SelectedApplication.Application.Id, status, cancellationToken);
            await ReloadListsAsync(cancellationToken);
            StatusMessage = successMessage;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "操作已取消。";
            return false;
        }
        catch (Exception exception)
        {
            StatusMessage = $"投递状态更新失败：{exception.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> UpdateTaskAsync(
        JobAutomationTaskStatus status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (IsBusy || SelectedTask is null) return false;
        IsBusy = true;
        try
        {
            await jobPostingService.UpdateAutomationTaskStatusAsync(SelectedTask.Task.Id, status, cancellationToken);
            await ReloadListsAsync(cancellationToken);
            StatusMessage = successMessage;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "操作已取消。";
            return false;
        }
        catch (Exception exception)
        {
            StatusMessage = $"复核任务更新失败：{exception.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadListsAsync(CancellationToken cancellationToken)
    {
        var selectedApplicationId = SelectedApplication?.Application.Id;
        var selectedTaskId = SelectedTask?.Task.Id;
        var applicationsTask = jobRepository.ListApplicationsAsync(cancellationToken);
        var tasksTask = jobRepository.ListAutomationTasksAsync(cancellationToken);
        await Task.WhenAll(applicationsTask, tasksTask);
        var applications = await applicationsTask;
        var tasks = await tasksTask;

        Applications.Clear();
        foreach (var application in applications) Applications.Add(new ApplicationRow(application));
        ReplaceApplicationLanes();
        ReviewTasks.Clear();
        foreach (var task in tasks) ReviewTasks.Add(new ReviewTaskRow(task));
        ReplaceReviewLanes();
        SelectedApplication = Applications.FirstOrDefault(row => row.Application.Id == selectedApplicationId)
            ?? Applications.FirstOrDefault();
        SelectedTask = ReviewTasks.FirstOrDefault(row => row.Task.Id == selectedTaskId)
            ?? ReviewTasks.FirstOrDefault();
        SelectedApplicationStage = SelectedApplication is null
            ? ""
            : ToStageLabel(SelectedApplication.Application.Status);
        OnPropertyChanged(nameof(CanUpdateStage));
        EmptyApplicationsMessage = Applications.Count == 0 ? "还没有投递记录。可从岗位详情创建待确认记录。" : "";
        EmptyTasksMessage = ReviewTasks.Count == 0 ? "还没有复核任务。" : "";
    }

    private void ReplaceApplicationLanes()
    {
        TodoApplications.Clear();
        SubmittedApplications.Clear();
        ReadApplications.Clear();
        InterviewApplications.Clear();
        OfferApplications.Clear();
        ClosedApplications.Clear();

        foreach (var application in Applications)
        {
            var target = application.Application.Status switch
            {
                JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview => TodoApplications,
                JobApplicationStatus.Submitted => SubmittedApplications,
                JobApplicationStatus.Contacting => ReadApplications,
                JobApplicationStatus.WrittenTest or JobApplicationStatus.FirstInterview or JobApplicationStatus.SecondInterview or JobApplicationStatus.FinalInterview => InterviewApplications,
                JobApplicationStatus.Offer => OfferApplications,
                _ => ClosedApplications
            };
            target.Add(application);
        }
    }

    private void ReplaceReviewLanes()
    {
        QueuedReviewTasks.Clear();
        InReviewTasks.Clear();
        CompletedReviewTasks.Clear();

        foreach (var task in ReviewTasks)
        {
            var target = task.Task.Status switch
            {
                JobAutomationTaskStatus.Queued => QueuedReviewTasks,
                JobAutomationTaskStatus.InReview => InReviewTasks,
                JobAutomationTaskStatus.Completed => CompletedReviewTasks,
                _ => CompletedReviewTasks
            };
            target.Add(task);
        }
    }

    private static string ToStageLabel(JobApplicationStatus status) => status switch
    {
        JobApplicationStatus.Draft => "待确认",
        JobApplicationStatus.ReadyForReview => "待确认",
        JobApplicationStatus.Submitted => "已投递",
        JobApplicationStatus.Contacting => "沟通中",
        JobApplicationStatus.WrittenTest => "待笔试",
        JobApplicationStatus.FirstInterview => "一面",
        JobApplicationStatus.SecondInterview => "二面",
        JobApplicationStatus.FinalInterview => "终面",
        JobApplicationStatus.Offer => "Offer",
        JobApplicationStatus.Rejected => "已拒绝",
        JobApplicationStatus.Withdrawn => "主动放弃",
        JobApplicationStatus.Abandoned => "主动放弃",
        JobApplicationStatus.Failed => "失败",
        _ => "待确认"
    };

    private static bool TryParseStage(string? label, out JobApplicationStatus status)
    {
        status = label switch
        {
            "待确认" => JobApplicationStatus.ReadyForReview,
            "沟通中" => JobApplicationStatus.Contacting,
            "已投递" => JobApplicationStatus.Submitted,
            "待笔试" => JobApplicationStatus.WrittenTest,
            "一面" => JobApplicationStatus.FirstInterview,
            "二面" => JobApplicationStatus.SecondInterview,
            "终面" => JobApplicationStatus.FinalInterview,
            "Offer" => JobApplicationStatus.Offer,
            "已拒绝" => JobApplicationStatus.Rejected,
            "主动放弃" => JobApplicationStatus.Abandoned,
            _ => JobApplicationStatus.Failed
        };
        return label is "待确认" or "沟通中" or "已投递" or "待笔试" or "一面" or "二面" or "终面" or "Offer" or "已拒绝" or "主动放弃";
    }
}

public sealed record ApplicationRow(JobApplication Application)
{
    public string Title => Application.JobPosting.Title;

    public string Company => Application.JobPosting.Company;

    public string JobDescription => string.IsNullOrWhiteSpace(Application.JobPosting.Description)
        ? "未记录 JD"
        : Application.JobPosting.Description;

    public string CandidateName => Application.CandidateProfile?.Name ?? "未关联简历";

    public string ResumeName => CandidateName;

    public string StatusLabel => Application.Status switch
    {
        JobApplicationStatus.Draft => "草稿",
        JobApplicationStatus.ReadyForReview => "待确认",
        JobApplicationStatus.Submitted => "已投递",
        JobApplicationStatus.Contacting => "沟通中",
        JobApplicationStatus.WrittenTest => "待笔试",
        JobApplicationStatus.FirstInterview => "一面",
        JobApplicationStatus.SecondInterview => "二面",
        JobApplicationStatus.FinalInterview => "终面",
        JobApplicationStatus.Offer => "Offer",
        JobApplicationStatus.Rejected => "已拒绝",
        JobApplicationStatus.Abandoned => "主动放弃",
        JobApplicationStatus.Withdrawn => "已撤销",
        JobApplicationStatus.Failed => "失败",
        _ => "未知状态"
    };

    public string UpdatedAtLabel => Application.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}

public sealed record ReviewTaskRow(JobAutomationTask Task)
{
    public string Title => Task.JobPosting.Title;

    public string Company => Task.JobPosting.Company;

    public string StatusLabel => Task.Status switch
    {
        JobAutomationTaskStatus.Queued => "待复核",
        JobAutomationTaskStatus.InReview => "复核中",
        JobAutomationTaskStatus.Completed => "已完成",
        JobAutomationTaskStatus.Cancelled => "已取消",
        JobAutomationTaskStatus.Failed => "失败",
        _ => "未知状态"
    };

    public string CreatedAtLabel => Task.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}
