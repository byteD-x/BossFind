using System.Collections.ObjectModel;
using System.Globalization;
using BossFind.Application.Jobs;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Platform.Boss.WebView;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class DashboardViewModel(
    CandidateProfileService profileService,
    IJobRepository jobRepository) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string ProfileCount { get; set; } = "0";

    [ObservableProperty]
    public partial string ConfirmedFactCount { get; set; } = "0";

    [ObservableProperty]
    public partial string SavedJobCount { get; set; } = "0";

    [ObservableProperty]
    public partial string FavoriteJobCount { get; set; } = "0";

    [ObservableProperty]
    public partial string ApplicationCount { get; set; } = "0";

    [ObservableProperty]
    public partial string SubmittedApplicationCount { get; set; } = "0";

    [ObservableProperty]
    public partial string PendingActionCount { get; set; } = "0";

    [ObservableProperty]
    public partial string WorkflowProgress { get; set; } = "0/5";

    [ObservableProperty]
    public partial double WorkflowProgressValue { get; set; }

    [ObservableProperty]
    public partial string NextStepTitle { get; set; } = "先建立求职者简历";

    [ObservableProperty]
    public partial string NextStepDescription { get; set; } = "填写基本信息和经历，后续匹配岗位时可以直接复用。";

    [ObservableProperty]
    public partial string NextStepActionLabel { get; set; } = "完善档案";

    [ObservableProperty]
    public partial string NextStepRoute { get; set; } = "profiles";

    [ObservableProperty]
    public partial string NextStepUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmptyJobsMessage { get; set; } = "";

    public ObservableCollection<JobPosting> RecentPostings { get; } = [];

    public ObservableCollection<WorkflowStep> WorkflowSteps { get; } = [];

    public ObservableCollection<ReadinessItem> ReadinessItems { get; } = [];

    public ObservableCollection<DashboardTrendPoint> ApplicationTrend { get; } = [];

    public ObservableCollection<DashboardFunnelStep> ApplicationFunnel { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var profilesTask = profileService.ListAsync(cancellationToken: cancellationToken);
            var postingsTask = jobRepository.ListAsync(cancellationToken: cancellationToken);
            var applicationsTask = jobRepository.ListApplicationsAsync(cancellationToken);
            var tasksTask = jobRepository.ListAutomationTasksAsync(cancellationToken);
            await Task.WhenAll(profilesTask, postingsTask, applicationsTask, tasksTask);

            var profiles = await profilesTask;
            var postings = await postingsTask;
            var applications = await applicationsTask;
            var tasks = await tasksTask;
            var confirmedFactCount = profiles.Sum(profile => profile.Facts.Count(fact => fact.IsConfirmed));
            var favoriteJobCount = postings.Count(posting => posting.IsFavorite);
            var applicationCount = applications.Count;
            var submittedApplicationCount = applications.Count(application => application.Status == JobApplicationStatus.Submitted);
            var preferredPosting = postings.Count > 0 ? postings[0] : null;
            foreach (var posting in postings)
            {
                if (posting.IsFavorite)
                {
                    preferredPosting = posting;
                    break;
                }
            }
            var pendingActions = applications.Count(application => application.Status is JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview)
                + tasks.Count(task => task.Status is JobAutomationTaskStatus.Queued or JobAutomationTaskStatus.InReview);

            ProfileCount = profiles.Count.ToString(CultureInfo.CurrentCulture);
            ConfirmedFactCount = confirmedFactCount.ToString(CultureInfo.CurrentCulture);
            SavedJobCount = postings.Count.ToString(CultureInfo.CurrentCulture);
            FavoriteJobCount = favoriteJobCount.ToString(CultureInfo.CurrentCulture);
            ApplicationCount = applicationCount.ToString(CultureInfo.CurrentCulture);
            SubmittedApplicationCount = submittedApplicationCount.ToString(CultureInfo.CurrentCulture);
            PendingActionCount = pendingActions.ToString(CultureInfo.CurrentCulture);
            ReplaceWorkflowSteps(
                profiles.Count > 0 && confirmedFactCount > 0,
                postings.Count > 0,
                favoriteJobCount > 0,
                submittedApplicationCount > 0,
                submittedApplicationCount > 0 && pendingActions == 0,
                pendingActions,
                preferredPosting);
            ReplaceApplicationCharts(applications);
            ReplaceRecentPostings(postings.Take(5));
            ReplaceReadinessItems(WebView2RuntimeProbe.GetStatus());
            StatusMessage = "工作台数据已更新。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "刷新已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"工作台加载失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceRecentPostings(IEnumerable<JobPosting> postings)
    {
        RecentPostings.Clear();
        foreach (var posting in postings)
        {
            RecentPostings.Add(posting);
        }

        EmptyJobsMessage = RecentPostings.Count == 0 ? "还没有岗位记录。打开浏览器并查看一个岗位后，这里会显示最近记录。" : "";
    }

    private void ReplaceWorkflowSteps(
        bool profileReady,
        bool hasPostings,
        bool hasFavorites,
        bool hasSubmittedApplications,
        bool hasNoPendingActions,
        int pendingActions,
        JobPosting? preferredPosting)
    {
        var definitions = new[]
        {
            new WorkflowStepDefinition("01", "准备求职者简历", "补充基本信息、经历和技能，后续匹配岗位时可以直接复用。", profileReady, "完善简历", "profiles"),
            new WorkflowStepDefinition("02", "发现合适岗位", "在招聘浏览器中查看岗位详情，确认后再记录岗位。", hasPostings, "打开浏览器", "browser"),
            new WorkflowStepDefinition("03", "筛选并匹配岗位", "收藏值得申请的岗位，在岗位详情中查看 JD 和简历匹配度。", hasFavorites, "打开浏览器", "browser"),
            new WorkflowStepDefinition("04", "确认并完成投递", "从岗位详情打开投递页，在平台内核对信息后手动提交。", hasSubmittedApplications, "开始投递", "browser"),
            new WorkflowStepDefinition("05", "跟进投递进度", "集中处理待确认投递和人工复核任务，保持每条记录有结果。", hasNoPendingActions && hasSubmittedApplications, "查看投递", "applications")
        };
        var nextIndex = Array.FindIndex(definitions, definition => !definition.IsComplete);
        WorkflowSteps.Clear();
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            var status = definition.IsComplete
                ? "已完成"
                : index == nextIndex ? "现在处理" : "待开始";
            var description = definition.Key == "05" && pendingActions > 0
                ? $"还有 {pendingActions} 项待处理，建议先完成这些记录。"
                : definition.Description;
            WorkflowSteps.Add(new WorkflowStep(
                definition.Key,
                definition.Title,
                description,
                status,
                definition.ActionLabel,
                definition.Route,
                index == nextIndex));
        }

        WorkflowProgress = $"{definitions.Count(definition => definition.IsComplete)}/{definitions.Length}";
        WorkflowProgressValue = definitions.Length == 0
            ? 0
            : definitions.Count(definition => definition.IsComplete) * 100d / definitions.Length;
        if (nextIndex >= 0)
        {
            var next = definitions[nextIndex];
            NextStepTitle = next.Title;
            NextStepDescription = nextIndex == 4 && pendingActions > 0
                ? $"还有 {pendingActions} 项待处理，先把投递和复核记录收好。"
                : next.Description;
            NextStepActionLabel = next.ActionLabel;
            NextStepRoute = next.Route;
            NextStepUrl = next.Route == "browser" ? preferredPosting?.Url ?? string.Empty : string.Empty;
            return;
        }

        NextStepTitle = "继续跟进投递";
        NextStepDescription = "当前流程已跑通。定期更新投递状态，把新的岗位加入同一条工作流。";
        NextStepActionLabel = "查看投递";
        NextStepRoute = "applications";
        NextStepUrl = string.Empty;
    }

    private void ReplaceReadinessItems(WebView2RuntimeStatus runtime)
    {
        ReadinessItems.Clear();
        ReadinessItems.Add(new ReadinessItem(
            "WebView2 运行时",
            runtime.IsAvailable ? $"可用（{runtime.Version}）" : $"不可用：{runtime.Error}"));
    }

    private void ReplaceApplicationCharts(IReadOnlyList<JobApplication> applications)
    {
        ApplicationTrend.Clear();
        var today = DateTime.Now.Date;
        var trend = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(day => new
            {
                Day = day,
                Count = applications.Count(application => application.CreatedAtUtc.ToLocalTime().Date == day)
            })
            .ToArray();
        var maximum = Math.Max(1, trend.Max(item => item.Count));
        foreach (var item in trend)
        {
            ApplicationTrend.Add(new DashboardTrendPoint(
                item.Day.ToString("M/d", CultureInfo.CurrentCulture),
                item.Count,
                item.Count == 0 ? 8 : 18 + item.Count * 82d / maximum));
        }

        var total = applications.Count;
        var submitted = applications.Count(application => application.Status is not (JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview));
        var interviewing = applications.Count(application => application.Status is JobApplicationStatus.FirstInterview or JobApplicationStatus.SecondInterview or JobApplicationStatus.FinalInterview);
        var offers = applications.Count(application => application.Status == JobApplicationStatus.Offer);
        ApplicationFunnel.Clear();
        foreach (var item in new[]
        {
            new DashboardFunnelStep("全部投递", total),
            new DashboardFunnelStep("已投递", submitted),
            new DashboardFunnelStep("面试中", interviewing),
            new DashboardFunnelStep("Offer", offers)
        })
        {
            ApplicationFunnel.Add(item with { Percentage = total == 0 ? 0 : item.Count * 100d / total });
        }
    }
}

public sealed record ReadinessItem(string Name, string Status);

public sealed record WorkflowStep(
    string Number,
    string Title,
    string Description,
    string StatusLabel,
    string ActionLabel,
    string Route,
    bool IsCurrent)
{
    public bool IsComplete => StatusLabel == "已完成";
}

public sealed record DashboardTrendPoint(string DayLabel, int Count, double BarHeight);

public sealed record DashboardFunnelStep(string Label, int Count)
{
    public double Percentage { get; init; }

    public string CountLabel => Count.ToString(CultureInfo.CurrentCulture);
}

internal sealed record WorkflowStepDefinition(
    string Key,
    string Title,
    string Description,
    bool IsComplete,
    string ActionLabel,
    string Route);
