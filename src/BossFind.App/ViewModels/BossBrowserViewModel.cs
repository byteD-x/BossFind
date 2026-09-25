using BossFind.Application.Profiles;
using BossFind.Application.Matching;
using BossFind.Application.Insights;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace BossFind.App.ViewModels;

public sealed partial class BossBrowserViewModel(
    JobCandidateImportService importService,
    CandidateProfileService profileService,
    IResumeJobMatchService? matchService = null)
    : ObservableObject
{
    [ObservableProperty]
    public partial string CandidateName { get; set; } = "待补充求职者简历";

    [ObservableProperty]
    public partial string ResumeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResumeStatus { get; set; } = "选择档案后可生成简历分块。";

    [ObservableProperty]
    public partial string Status { get; set; } = "等待初始化 WebView2";

    [ObservableProperty]
    public partial int SearchResultCount { get; set; }

    [ObservableProperty]
    public partial string SearchResultStatus { get; set; } = "";

    public bool HasSearchResults => SearchResultCount > 0;

    partial void OnSearchResultCountChanged(int value) => OnPropertyChanged(nameof(HasSearchResults));

    public void SetSearchResults(int count)
    {
        SearchResultCount = count;
        SearchResultStatus = count == 0
            ? string.Empty
            : $"当前页面识别到 {count} 个岗位，可批量保存到岗位记录。";
    }

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
    public partial string JobQualityText { get; set; } = "岗位信息完整度待分析。";

    [ObservableProperty]
    public partial string JobQualityMissingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string JobRiskFlagsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial bool IsPostingSaved { get; set; }

    public string SavePostingLabel => IsPostingSaved ? "已记录岗位" : "记录岗位";

    [ObservableProperty]
    public partial string GreetingText { get; set; } = "选择简历后生成打招呼语。";

    public string SelectedMatchScoreText => GetSelectedMatch()?.ScoreText ?? "待分析";

    public string SelectedMatchExplanation => GetSelectedMatch()?.Result.Explanation
        ?? (SelectedCandidateProfile is null ? "请选择一份简历。" : "点击“分析匹配度”生成结果。");

    public string SelectedMatchedSkillsText => GetSelectedMatch()?.MatchedSkillsText ?? "待分析";

    public string SelectedMissingSkillsText => GetSelectedMatch()?.MissingSkillsText ?? "待分析";

    public string FavoriteActionLabel => IsFavorite ? "取消收藏" : "收藏岗位";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteActionLabel));

    partial void OnIsPostingSavedChanged(bool value) => OnPropertyChanged(nameof(SavePostingLabel));

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
        RefreshGreeting();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowReloadIcon));
        OnPropertyChanged(nameof(ShowStopIcon));
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string MatchStatus { get; set; } = "尚未生成简历匹配度。";

    public ObservableCollection<JobMatchItem> Matches { get; } = [];

    public ObservableCollection<CandidateProfile> CandidateProfiles { get; } = [];

    public ObservableCollection<ResumeChunk> ResumeChunks { get; } = [];

    [ObservableProperty]
    public partial CandidateProfile? SelectedCandidateProfile { get; set; }

    partial void OnSelectedCandidateProfileChanged(CandidateProfile? value)
    {
        ReplaceResumeChunks(value);
        RefreshGreeting();
        OnPropertyChanged(nameof(SelectedMatchScoreText));
        OnPropertyChanged(nameof(SelectedMatchExplanation));
        OnPropertyChanged(nameof(SelectedMatchedSkillsText));
        OnPropertyChanged(nameof(SelectedMissingSkillsText));
    }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
        CandidateProfiles.Clear();
        foreach (var profile in profiles)
        {
            CandidateProfiles.Add(profile);
        }

        SelectedCandidateProfile ??= CandidateProfiles.FirstOrDefault();
        ReplaceResumeChunks(SelectedCandidateProfile);
    }

    public void ExtractResumeChunks()
    {
        ReplaceResumeChunks(SelectedCandidateProfile);
    }

    public void GenerateGreeting()
    {
        RefreshGreeting();
    }

    private void ReplaceResumeChunks(CandidateProfile? profile)
    {
        ResumeChunks.Clear();
        if (profile is null)
        {
            ResumeStatus = "请先选择一份求职者简历。";
            return;
        }

        foreach (var chunk in ResumeChunkExtractor.Extract(profile, ResumeText))
        {
            ResumeChunks.Add(chunk);
        }

        ResumeStatus = ResumeChunks.Count == 0
            ? "没有识别到可用内容，请粘贴简历原文或补充档案事实。"
            : $"已生成 {ResumeChunks.Count} 个简历分块。可复制，或在 BOSS 在线简历编辑页逐块填入。";
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
        var quality = JobPostingQualityAnalyzer.Analyze(new JobPosting
        {
            Title = JobTitle,
            Company = Company,
            City = City,
            Salary = Salary,
            Experience = Experience,
            Education = Education,
            Benefits = Benefits,
            Skills = Tags,
            Description = Description
        });
        JobQualityText = quality.ScoreLabel;
        JobQualityMissingText = quality.MissingFieldsLabel;
        JobRiskFlagsText = quality.RiskFlagsLabel;
        Matches.Clear();
        MatchStatus = IsSummaryReady ? "尚未生成简历匹配度。" : "请先加载有效的岗位摘要。";
        RefreshGreeting();
        OnPropertyChanged(nameof(SelectedMatchScoreText));
        OnPropertyChanged(nameof(SelectedMatchExplanation));
        OnPropertyChanged(nameof(SelectedMatchedSkillsText));
        OnPropertyChanged(nameof(SelectedMissingSkillsText));
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
            var matches = (await Task.WhenAll(profiles
                .Select(async profile => new JobMatchItem(profile, await MatchProfileAsync(profile, requirement, cancellationToken)))))
                .OrderByDescending(match => match.Result.Score)
                .ThenBy(match => match.Profile.Name, StringComparer.Ordinal)
                .ToArray();

            Matches.Clear();
            foreach (var match in matches)
            {
                Matches.Add(match);
            }

            MatchStatus = Matches.Count == 0
                ? "暂无可用简历，请先在求职者简历中创建档案。"
                : $"已为 {Matches.Count} 份简历生成匹配度。";
            OnPropertyChanged(nameof(SelectedMatchScoreText));
            OnPropertyChanged(nameof(SelectedMatchExplanation));
            OnPropertyChanged(nameof(SelectedMatchedSkillsText));
            OnPropertyChanged(nameof(SelectedMissingSkillsText));
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

    private Task<JobMatchResult> MatchProfileAsync(
        CandidateProfile profile,
        JobRequirement requirement,
        CancellationToken cancellationToken)
    {
        return matchService is null
            ? Task.FromResult(CandidateJobMatcher.Match(profile, requirement))
            : matchService.MatchAsync(profile, requirement, cancellationToken);
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
            Status = $"已导入求职者简历：{profile.Name}";
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

    private JobMatchItem? GetSelectedMatch()
    {
        var selectedProfileId = SelectedCandidateProfile?.Id;
        return selectedProfileId is null
            ? null
            : Matches.FirstOrDefault(match => match.Profile.Id == selectedProfileId.Value);
    }

    private void RefreshGreeting()
    {
        if (!IsSummaryReady)
        {
            GreetingText = "打开岗位详情后生成打招呼语。";
            return;
        }

        if (SelectedCandidateProfile is null)
        {
            GreetingText = "请选择一份简历，系统会根据 JD 和简历生成打招呼语。";
            return;
        }

        var name = string.IsNullOrWhiteSpace(SelectedCandidateProfile.Name)
            ? "您好"
            : $"您好，我是{SelectedCandidateProfile.Name}";
        var matchedSkills = GetSelectedMatch()?.MatchedSkillsText;
        var skillText = string.IsNullOrWhiteSpace(matchedSkills) || matchedSkills == "无"
            ? ""
            : $"，我有{matchedSkills}等相关经验";
        var companyText = string.IsNullOrWhiteSpace(Company) ? "贵公司" : Company;
        GreetingText = $"{name}，看到{companyText}的{JobTitle}岗位，{skillText}，希望有机会进一步沟通。";
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
