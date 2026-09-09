using System.Collections.ObjectModel;
using System.Globalization;
using BossFind.Application.Profiles;
using BossFind.Platform.Boss.WebView;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class DashboardViewModel(CandidateProfileService profileService)
    : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "等待刷新工作台数据。";

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
            var confirmedFactCount = 0;
            DateTime? latestUpdate = profiles.Count == 0
                ? null
                : profiles.Max(profile => profile.UpdatedAtUtc);

            foreach (var profile in profiles)
            {
                var loadedProfile = await profileService.GetAsync(profile.Id, cancellationToken);
                if (loadedProfile is null)
                {
                    continue;
                }

                confirmedFactCount += loadedProfile.Facts.Count(fact => fact.IsConfirmed);
                if (latestUpdate is null || loadedProfile.UpdatedAtUtc > latestUpdate)
                {
                    latestUpdate = loadedProfile.UpdatedAtUtc;
                }
            }

            var runtime = WebView2RuntimeProbe.GetStatus();
            ReplaceReadinessItems(profiles.Count, confirmedFactCount, latestUpdate, runtime);
            StatusMessage = $"已刷新：{profiles.Count} 个候选人档案。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "刷新已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceReadinessItems(
        int profileCount,
        int confirmedFactCount,
        DateTime? latestUpdate,
        WebView2RuntimeStatus runtime)
    {
        ReadinessItems.Clear();
        ReadinessItems.Add(new ReadinessItem("候选人档案", $"{profileCount} 个"));
        ReadinessItems.Add(new ReadinessItem("已确认事实", $"{confirmedFactCount} 条"));
        ReadinessItems.Add(new ReadinessItem(
            "最近更新",
            latestUpdate is null
                ? "暂无数据"
                : latestUpdate.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        ReadinessItems.Add(new ReadinessItem(
            "WebView2 运行时",
            runtime.IsAvailable
                ? $"可用（{runtime.Version}）"
                : $"不可用：{runtime.Error}"));
        ReadinessItems.Add(new ReadinessItem("平台访问", "仅加载本地固定测试页，不执行平台操作"));
    }
}

public sealed record ReadinessItem(string Name, string Status);
