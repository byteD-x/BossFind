using System.Collections.ObjectModel;
using BossFind.Application.Insights;
using BossFind.Application.Jobs;
using BossFind.Application.Matching;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class InsightsViewModel(
    IJobRepository jobRepository,
    CandidateProfileService profileService,
    IJobInsightService insightService) : ObservableObject
{
    public ObservableCollection<JobPosting> Postings { get; } = [];

    public ObservableCollection<CandidateProfile> CandidateProfiles { get; } = [];

    public ObservableCollection<string> Suggestions { get; } = [];

    [ObservableProperty]
    public partial JobPosting? SelectedPosting { get; set; }

    [ObservableProperty]
    public partial CandidateProfile? SelectedCandidateProfile { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string MatchSummary { get; set; } = "选择简历后可查看技能匹配。";

    [ObservableProperty]
    public partial string MatchedSkills { get; set; } = "";

    [ObservableProperty]
    public partial string MissingSkills { get; set; } = "";

    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = "";

    [ObservableProperty]
    public partial string QualityScoreText { get; set; } = "岗位信息完整度待分析。";

    [ObservableProperty]
    public partial string QualityMissingText { get; set; } = "";

    [ObservableProperty]
    public partial string QualityRiskText { get; set; } = "";

    public bool HasSelectedPosting => SelectedPosting is not null;

    partial void OnSelectedPostingChanged(JobPosting? value)
    {
        OnPropertyChanged(nameof(HasSelectedPosting));
        RefreshQuality(value);
        Summary = "";
        Suggestions.Clear();
        MatchSummary = "选择简历后可查看技能匹配。";
        MatchedSkills = "";
        MissingSkills = "";
    }

    private void RefreshQuality(JobPosting? posting)
    {
        if (posting is null)
        {
            QualityScoreText = "岗位信息完整度待分析。";
            QualityMissingText = "";
            QualityRiskText = "";
            return;
        }

        var quality = JobPostingQualityAnalyzer.Analyze(posting);
        QualityScoreText = quality.ScoreLabel;
        QualityMissingText = quality.MissingFieldsLabel;
        QualityRiskText = quality.RiskFlagsLabel;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var selectedPostingId = SelectedPosting?.Id;
            var selectedProfileId = SelectedCandidateProfile?.Id;
            var postingsTask = jobRepository.ListAsync(cancellationToken: cancellationToken);
            var profilesTask = profileService.ListAsync(cancellationToken: cancellationToken);
            await Task.WhenAll(postingsTask, profilesTask);
            var postings = await postingsTask;
            var profiles = await profilesTask;
            Postings.Clear();
            foreach (var posting in postings) Postings.Add(posting);
            CandidateProfiles.Clear();
            foreach (var profile in profiles) CandidateProfiles.Add(profile);
            SelectedPosting = Postings.FirstOrDefault(posting => posting.Id == selectedPostingId)
                ?? Postings.FirstOrDefault();
            SelectedCandidateProfile = CandidateProfiles.FirstOrDefault(profile => profile.Id == selectedProfileId);
            EmptyMessage = Postings.Count == 0 ? "还没有岗位记录。请先打开一个岗位，再回来查看岗位信息和简历匹配度。" : "";
            StatusMessage = $"可分析 {Postings.Count} 个已记录岗位。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "加载已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"岗位数据加载失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || SelectedPosting is null) return;
        IsBusy = true;
        try
        {
            var insight = await insightService.CreateAsync(SelectedPosting, cancellationToken);
            Summary = insight.Summary;
            Suggestions.Clear();
            foreach (var suggestion in insight.Suggestions) Suggestions.Add(suggestion);

            if (SelectedCandidateProfile is null)
            {
                MatchSummary = "选择简历后可查看技能匹配。";
                MatchedSkills = "";
                MissingSkills = "";
            }
            else
            {
                var profile = await profileService.GetAsync(SelectedCandidateProfile.Id, cancellationToken);
                if (profile is null)
                {
                    MatchSummary = "所选档案已不存在，请刷新后重试。";
                    MatchedSkills = "";
                    MissingSkills = "";
                }
                else
                {
                    var requirement = new JobRequirement(
                        SelectedPosting.Title,
                        SelectedPosting.Company,
                        SelectedPosting.City,
                        SplitSkills(SelectedPosting.Skills));
                    var result = CandidateJobMatcher.Match(profile, requirement);
                    MatchSummary = $"{profile.Name} 的简历匹配度：{result.Score:P0}。{result.Explanation}";
                    MatchedSkills = result.MatchedSkills.Count == 0 ? "无" : string.Join("、", result.MatchedSkills);
                    MissingSkills = result.MissingSkills.Count == 0 ? "无" : string.Join("、", result.MissingSkills);
                }
            }

            StatusMessage = $"分析完成 · {insight.Provider}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "分析已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"分析失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string[] SplitSkills(string skills) => skills.Split(
        '、',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
