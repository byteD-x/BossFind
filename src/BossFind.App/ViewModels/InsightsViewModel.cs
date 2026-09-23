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
    public partial string MatchSummary { get; set; } = "选择候选人档案后可查看技能匹配。";

    [ObservableProperty]
    public partial string MatchedSkills { get; set; } = "";

    [ObservableProperty]
    public partial string MissingSkills { get; set; } = "";

    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = "";

    public bool HasSelectedPosting => SelectedPosting is not null;

    partial void OnSelectedPostingChanged(JobPosting? value)
    {
        OnPropertyChanged(nameof(HasSelectedPosting));
        Summary = "";
        Suggestions.Clear();
        MatchSummary = "选择候选人档案后可查看技能匹配。";
        MatchedSkills = "";
        MissingSkills = "";
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var selectedPostingId = SelectedPosting?.Id;
            var selectedProfileId = SelectedCandidateProfile?.Id;
            var postings = await jobRepository.ListAsync(cancellationToken: cancellationToken);
            var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
            Postings.Clear();
            foreach (var posting in postings) Postings.Add(posting);
            CandidateProfiles.Clear();
            foreach (var profile in profiles) CandidateProfiles.Add(profile);
            SelectedPosting = Postings.FirstOrDefault(posting => posting.Id == selectedPostingId)
                ?? Postings.FirstOrDefault();
            SelectedCandidateProfile = CandidateProfiles.FirstOrDefault(profile => profile.Id == selectedProfileId);
            EmptyMessage = Postings.Count == 0 ? "还没有岗位记录。请先打开一个岗位，再回来查看岗位信息和候选人匹配。" : "";
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
                MatchSummary = "选择候选人档案后可查看技能匹配。";
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
                    MatchSummary = $"{profile.Name} 的档案匹配度：{result.Score:P0}。{result.Explanation}";
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
