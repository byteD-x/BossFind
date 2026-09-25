using System.Collections.ObjectModel;
using BossFind.Application.Jobs;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class JobsViewModel(
    IJobRepository jobRepository,
    JobPostingService jobPostingService,
    CandidateProfileService profileService) : ObservableObject
{
    public ObservableCollection<JobPosting> Postings { get; } = [];

    public ObservableCollection<JobPosting> FilteredPostings { get; } = [];

    public ObservableCollection<CandidateProfile> CandidateProfiles { get; } = [];

    public IReadOnlyList<string> Filters { get; } = ["全部岗位", "仅收藏"];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "全部岗位";

    [ObservableProperty]
    public partial JobPosting? SelectedPosting { get; set; }

    [ObservableProperty]
    public partial CandidateProfile? SelectedCandidateProfile { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = "";

    public string FavoriteActionLabel => SelectedPosting?.IsFavorite == true ? "取消收藏" : "收藏岗位";

    public bool HasSelectedPosting => SelectedPosting is not null;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedPostingChanged(JobPosting? value)
    {
        OnPropertyChanged(nameof(FavoriteActionLabel));
        OnPropertyChanged(nameof(HasSelectedPosting));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
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
            SelectedCandidateProfile = CandidateProfiles.FirstOrDefault(profile => profile.Id == selectedProfileId);
            ApplyFilter();
            StatusMessage = $"已加载 {postings.Count} 个岗位记录。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "加载已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"岗位记录加载失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ToggleFavoriteAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || SelectedPosting is null) return;
        IsBusy = true;
        try
        {
            var isFavorite = !SelectedPosting.IsFavorite;
            await jobPostingService.SetFavoriteAsync(SelectedPosting, isFavorite, cancellationToken);
            SelectedPosting.IsFavorite = isFavorite;
            OnPropertyChanged(nameof(FavoriteActionLabel));
            ApplyFilter();
            StatusMessage = isFavorite ? "岗位已收藏。" : "已取消收藏。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "操作已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"收藏状态更新失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task QueueAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || SelectedPosting is null) return;
        IsBusy = true;
        try
        {
            await jobPostingService.QueueAsync(SelectedPosting, SelectedCandidateProfile?.Id, cancellationToken);
            StatusMessage = "已加入人工复核队列。";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "操作已取消。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加入队列失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var previousId = SelectedPosting?.Id;
        var search = SearchText.Trim();
        var filtered = Postings.Where(posting =>
            (SelectedFilter != "仅收藏" || posting.IsFavorite)
            && (search.Length == 0
                || posting.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || posting.Company.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || posting.City.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || posting.Skills.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .ToArray();

        FilteredPostings.Clear();
        foreach (var posting in filtered) FilteredPostings.Add(posting);
        SelectedPosting = filtered.FirstOrDefault(posting => posting.Id == previousId) ?? filtered.FirstOrDefault();
        EmptyMessage = Postings.Count == 0
            ? "还没有岗位记录。请先在招聘浏览器中打开一个岗位。"
            : filtered.Length == 0 ? "没有符合条件的岗位。" : "";
    }
}
