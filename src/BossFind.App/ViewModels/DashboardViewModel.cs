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
    public partial string PendingActionCount { get; set; } = "0";

    [ObservableProperty]
    public partial string EmptyJobsMessage { get; set; } = "";

    public ObservableCollection<JobPosting> RecentPostings { get; } = [];

    public ObservableCollection<ReadinessItem> ReadinessItems { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
            var postings = await jobRepository.ListAsync(cancellationToken: cancellationToken);
            var applications = await jobRepository.ListApplicationsAsync(cancellationToken);
            var tasks = await jobRepository.ListAutomationTasksAsync(cancellationToken);
            var pendingActions = applications.Count(application => application.Status is JobApplicationStatus.Draft or JobApplicationStatus.ReadyForReview)
                + tasks.Count(task => task.Status is JobAutomationTaskStatus.Queued or JobAutomationTaskStatus.InReview);

            ProfileCount = profiles.Count.ToString(CultureInfo.CurrentCulture);
            ConfirmedFactCount = profiles.Sum(profile => profile.Facts.Count(fact => fact.IsConfirmed)).ToString(CultureInfo.CurrentCulture);
            SavedJobCount = postings.Count.ToString(CultureInfo.CurrentCulture);
            PendingActionCount = pendingActions.ToString(CultureInfo.CurrentCulture);
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

    private void ReplaceReadinessItems(WebView2RuntimeStatus runtime)
    {
        ReadinessItems.Clear();
        ReadinessItems.Add(new ReadinessItem(
            "WebView2 运行时",
            runtime.IsAvailable ? $"可用（{runtime.Version}）" : $"不可用：{runtime.Error}"));
    }
}

public sealed record ReadinessItem(string Name, string Status);
