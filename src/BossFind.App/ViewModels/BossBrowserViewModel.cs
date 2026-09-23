using BossFind.Application.Profiles;
using BossFind.Application.Matching;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace BossFind.App.ViewModels;

public sealed partial class BossBrowserViewModel(
    JobCandidateImportService importService,
    CandidateProfileService profileService)
    : ObservableObject
{
    [ObservableProperty]
    public partial string CandidateName { get; set; } = "待补充候选人";

    [ObservableProperty]
    public partial string Status { get; set; } = "等待初始化 WebView2";

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "尚未打开网页";

    [ObservableProperty]
    public partial string CurrentUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageMetaDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageSiteName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageCanonicalUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageKeywords { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageAuthor { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageFaviconUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageLanguage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int PageTextLength { get; set; }

    [ObservableProperty]
    public partial int PageLinksCount { get; set; }

    [ObservableProperty]
    public partial double ZoomFactor { get; set; } = 1.0;

    public bool HasPageMetadata => !string.IsNullOrWhiteSpace(CurrentUrl);

    public string ZoomText => $"{ZoomFactor:P0}";

    public string PageStatsText => $"正文约 {PageTextLength:N0} 字 · {PageLinksCount:N0} 个链接";

    [ObservableProperty]
    public partial bool CanGoBack { get; set; }

    [ObservableProperty]
    public partial bool CanGoForward { get; set; }

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
    public partial string Salary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Experience { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Education { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Benefits { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public string FavoriteActionLabel => IsFavorite ? "取消收藏" : "收藏岗位";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteActionLabel));

    partial void OnCurrentUrlChanged(string value) => OnPropertyChanged(nameof(HasPageMetadata));

    partial void OnZoomFactorChanged(double value) => OnPropertyChanged(nameof(ZoomText));

    partial void OnPageTextLengthChanged(int value) => OnPropertyChanged(nameof(PageStatsText));

    partial void OnPageLinksCountChanged(int value) => OnPropertyChanged(nameof(PageStatsText));

    [ObservableProperty]
    public partial bool IsSummaryReady { get; set; }

    // 供 x:Bind 直接控制可见性的成对状态（true → Visible）
    public bool ShowSummary => IsSummaryReady;

    public bool ShowSummaryHint => !IsSummaryReady;

    public bool ShowReloadIcon => !IsLoading;

    public bool ShowStopIcon => IsLoading;

    partial void OnIsSummaryReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSummary));
        OnPropertyChanged(nameof(ShowSummaryHint));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowReloadIcon));
        OnPropertyChanged(nameof(ShowStopIcon));
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string MatchStatus { get; set; } = "尚未匹配候选人档案。";

    public ObservableCollection<JobMatchItem> Matches { get; } = [];

    public ObservableCollection<CandidateProfile> CandidateProfiles { get; } = [];

    [ObservableProperty]
    public partial CandidateProfile? SelectedCandidateProfile { get; set; }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
        CandidateProfiles.Clear();
        foreach (var profile in profiles)
        {
            CandidateProfiles.Add(profile);
        }

        SelectedCandidateProfile ??= CandidateProfiles.FirstOrDefault();
    }

    public void SetJobSummary(
        string title,
        string company,
        string city,
        IReadOnlyList<string> tags,
        string salary = "",
        string experience = "",
        string education = "",
        IReadOnlyList<string>? benefits = null,
        string description = "")
    {
        JobTitle = title;
        Company = company;
        City = city;
        Tags = string.Join("、", tags);
        Salary = salary;
        Experience = experience;
        Education = education;
        Benefits = string.Join("、", benefits ?? []);
        Description = description;
        IsSummaryReady = !string.IsNullOrWhiteSpace(JobTitle);
        Matches.Clear();
        MatchStatus = IsSummaryReady ? "尚未匹配候选人档案。" : "请先加载有效的岗位摘要。";
    }

    public async Task MatchAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || !IsSummaryReady)
        {
            if (!IsBusy)
            {
                MatchStatus = "请先加载有效的岗位摘要。";
            }

            return;
        }

        IsBusy = true;
        try
        {
            var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
            var requirement = new JobRequirement(
                JobTitle,
                Company,
                City,
                GetSkills());
            var matches = profiles
                .Select(profile => new JobMatchItem(profile, CandidateJobMatcher.Match(profile, requirement)))
                .OrderByDescending(match => match.Result.Score)
                .ThenBy(match => match.Profile.Name, StringComparer.Ordinal)
                .ToArray();

            Matches.Clear();
            foreach (var match in matches)
            {
                Matches.Add(match);
            }

            MatchStatus = Matches.Count == 0
                ? "暂无候选人档案，请先在档案页创建。"
                : $"已匹配 {Matches.Count} 个候选人档案。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MatchStatus = "匹配已取消。";
        }
        catch (Exception exception)
        {
            MatchStatus = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
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
            await LoadProfilesAsync(cancellationToken);
            SelectedCandidateProfile = CandidateProfiles.FirstOrDefault(item => item.Id == profile.Id);
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

    private string[] GetSkills()
    {
        return Tags.Split(
            '、',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

public sealed record JobMatchItem(
    CandidateProfile Profile,
    JobMatchResult Result)
{
    public string ScoreText => $"{Result.Score:P0}";

    public string MatchedSkillsText => Result.MatchedSkills.Count == 0
        ? "无"
        : string.Join("、", Result.MatchedSkills);

    public string MissingSkillsText => Result.MissingSkills.Count == 0
        ? "无"
        : string.Join("、", Result.MissingSkills);

    public string MatchedSkillsLabel => $"命中技能：{MatchedSkillsText}";

    public string MissingSkillsLabel => $"缺失技能：{MissingSkillsText}";
}
