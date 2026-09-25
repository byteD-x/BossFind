using System.Collections.ObjectModel;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class CandidateProfilesViewModel(
    CandidateProfileService profileService,
    ResumeDocumentImportService? resumeImportService = null)
    : ObservableObject
{
    private CandidateProfile? editingProfile;
    private CandidateFact? editingFact;
    private CancellationTokenSource? searchCancellation;
    private int searchGeneration;

    public ObservableCollection<CandidateProfile> Profiles { get; } = [];

    public ObservableCollection<CandidateFact> Facts { get; } = [];

    public IReadOnlyList<CandidateFactCategory> Categories { get; } =
        Enum.GetValues<CandidateFactCategory>();

    public string FactActionLabel => IsEditingFact ? "保存事实" : "添加事实";

    public bool CanDeleteProfile => editingProfile is not null;

    public string SearchResultMessage => string.IsNullOrWhiteSpace(SearchText)
        ? $"共 {Profiles.Count} 个档案"
        : Profiles.Count == 0
            ? "未找到匹配档案"
            : $"找到 {Profiles.Count} 个档案";

    [ObservableProperty]
    public partial CandidateProfile? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(SearchResultMessage));
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Headline { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CandidateFactCategory SelectedFactCategory { get; set; } = CandidateFactCategory.Skill;

    [ObservableProperty]
    public partial string FactContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FactSourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal FactConfidence { get; set; }

    [ObservableProperty]
    public partial bool FactIsConfirmed { get; set; }

    [ObservableProperty]
    public partial bool IsEditingFact { get; set; }

    partial void OnIsEditingFactChanged(bool value)
    {
        OnPropertyChanged(nameof(FactActionLabel));
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "正在加载求职者简历。";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await RunBusyAsync(async () =>
            {
                var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
                ReplaceProfiles(profiles);
                if (Profiles.Count == 0)
                {
                    BeginNew();
                    StatusMessage = "还没有求职者简历。";
                    return;
                }

                await LoadProfileAsync(Profiles[0], cancellationToken);
            }, cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ImportDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (resumeImportService is null)
        {
            StatusMessage = "简历导入服务尚未配置。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var profile = await resumeImportService.ImportAsync(filePath, cancellationToken);
            await ReloadProfilesAsync(profile.Id, cancellationToken);
            StatusMessage = $"已导入简历并建立档案：{profile.Name}";
        }, cancellationToken);
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy && searchCancellation is null)
        {
            return;
        }

        searchCancellation?.Cancel();
        searchCancellation?.Dispose();
        var currentSearch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        searchCancellation = currentSearch;
        var searchText = SearchText;
        var requestGeneration = Interlocked.Increment(ref searchGeneration);
        IsBusy = true;

        try
        {
            var profiles = await profileService.ListAsync(
                searchText,
                currentSearch.Token);
            if (requestGeneration != searchGeneration)
            {
                return;
            }

            ReplaceProfiles(profiles);
            SelectSearchResultWithoutResettingDraft();
            StatusMessage = Profiles.Count == 0
                ? "没有找到匹配的求职者简历。"
                : $"已找到 {Profiles.Count} 份求职者简历。";
        }
        catch (OperationCanceledException) when (currentSearch.IsCancellationRequested)
        {
            if (requestGeneration == searchGeneration)
            {
                StatusMessage = "搜索已取消。";
            }
        }
        catch (Exception exception)
        {
            if (requestGeneration == searchGeneration)
            {
                StatusMessage = exception.Message;
            }
        }
        finally
        {
            if (requestGeneration == searchGeneration)
            {
                IsBusy = false;
                searchCancellation = null;
            }
        }
    }

    public Task ClearSearchAsync(CancellationToken cancellationToken = default)
    {
        SearchText = string.Empty;
        return SearchAsync(cancellationToken);
    }

    public async Task SelectProfileAsync(
        CandidateProfile? profile,
        CancellationToken cancellationToken = default)
    {
        if (profile is null)
        {
            BeginNew();
            return;
        }

        await RunBusyAsync(
            () => LoadProfileAsync(profile, cancellationToken),
            cancellationToken);
    }

    public void BeginNew()
    {
        SelectedProfile = null;
        editingProfile = null;
        OnPropertyChanged(nameof(CanDeleteProfile));
        Name = string.Empty;
        Headline = string.Empty;
        Location = string.Empty;
        Email = string.Empty;
        Phone = string.Empty;
        Facts.Clear();
        ResetFactDraft();
        StatusMessage = "新建求职者简历。";
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            if (editingProfile is null)
            {
                editingProfile = await profileService.CreateAsync(
                    Name,
                    Headline,
                    Location,
                    Email,
                    Phone,
                    cancellationToken);
                StatusMessage = "求职者简历已创建。";
            }
            else
            {
                editingProfile.Name = Name;
                editingProfile.Headline = Headline;
                editingProfile.Location = Location;
                editingProfile.Email = Email;
                editingProfile.Phone = Phone;
                await profileService.UpdateAsync(editingProfile, cancellationToken);
                StatusMessage = "求职者简历已保存。";
            }

            await ReloadProfilesAsync(editingProfile.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (editingProfile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await profileService.DeleteAsync(editingProfile.Id, cancellationToken);
            StatusMessage = "求职者简历已删除。";
            await ReloadProfilesAsync(null, cancellationToken);
        }, cancellationToken);
    }

    public void BeginFactEdit(CandidateFact? fact)
    {
        if (fact is null)
        {
            ResetFactDraft();
            return;
        }

        editingFact = fact;
        IsEditingFact = true;
        SelectedFactCategory = fact.Category;
        FactContent = fact.Content;
        FactSourceText = fact.SourceText;
        FactConfidence = fact.Confidence;
        FactIsConfirmed = fact.IsConfirmed;
        StatusMessage = "正在编辑事实。";
    }

    public async Task SaveFactAsync(CancellationToken cancellationToken = default)
    {
        if (editingProfile is null)
        {
            StatusMessage = "请先保存求职者简历，再添加内容。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (editingFact is null)
            {
                var fact = await profileService.AddFactAsync(
                    editingProfile.Id,
                    SelectedFactCategory,
                    FactContent,
                    FactSourceText,
                    FactConfidence,
                    FactIsConfirmed,
                    cancellationToken);
                Facts.Add(fact);
                StatusMessage = "简历信息已添加。";
            }
            else
            {
                editingFact.Category = SelectedFactCategory;
                editingFact.Content = FactContent;
                editingFact.SourceText = FactSourceText;
                editingFact.Confidence = FactConfidence;
                editingFact.IsConfirmed = FactIsConfirmed;
                await profileService.UpdateFactAsync(editingFact, cancellationToken);
                var factIndex = Facts.IndexOf(editingFact);
                if (factIndex >= 0)
                {
                    Facts.RemoveAt(factIndex);
                    Facts.Insert(factIndex, editingFact);
                }

                StatusMessage = "简历信息已保存。";
            }

            ResetFactDraft();
        }, cancellationToken);
    }

    public Task AddFactAsync(CancellationToken cancellationToken = default)
    {
        return SaveFactAsync(cancellationToken);
    }

    public async Task DeleteFactAsync(
        CandidateFact? fact,
        CancellationToken cancellationToken = default)
    {
        if (fact is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await profileService.DeleteFactAsync(fact.Id, cancellationToken);
            Facts.Remove(fact);
            if (ReferenceEquals(editingFact, fact))
            {
                ResetFactDraft();
            }

            StatusMessage = "简历信息已删除。";
        }, cancellationToken);
    }

    private async Task ReloadProfilesAsync(
        Guid? profileId,
        CancellationToken cancellationToken)
    {
        var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
        ReplaceProfiles(profiles);

        var selected = profileId is null
            ? Profiles.FirstOrDefault()
            : Profiles.FirstOrDefault(profile => profile.Id == profileId.Value);
        if (selected is null)
        {
            BeginNew();
            return;
        }

        await LoadProfileAsync(selected, cancellationToken);
    }

    private void ReplaceProfiles(IEnumerable<CandidateProfile> profiles)
    {
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        OnPropertyChanged(nameof(SearchResultMessage));
    }

    private void SelectSearchResultWithoutResettingDraft()
    {
        var currentProfileId = editingProfile?.Id;
        SelectedProfile = currentProfileId is null
            ? null
            : Profiles.FirstOrDefault(profile => profile.Id == currentProfileId.Value);
    }

    private async Task LoadProfileAsync(
        CandidateProfile profile,
        CancellationToken cancellationToken)
    {
        var loadedProfile = await profileService.GetAsync(profile.Id, cancellationToken);
        if (loadedProfile is null)
        {
            BeginNew();
            StatusMessage = "档案已不存在，请刷新列表。";
            return;
        }

        SelectedProfile = profile;
        editingProfile = loadedProfile;
        OnPropertyChanged(nameof(CanDeleteProfile));
        CopyProfileToDraft(loadedProfile);
        ReplaceFacts(loadedProfile.Facts);
        ResetFactDraft();
        StatusMessage = $"已加载 {loadedProfile.Name}。";
    }

    private void CopyProfileToDraft(CandidateProfile profile)
    {
        Name = profile.Name;
        Headline = profile.Headline;
        Location = profile.Location;
        Email = profile.Email;
        Phone = profile.Phone;
    }

    private void ReplaceFacts(IEnumerable<CandidateFact> facts)
    {
        Facts.Clear();
        foreach (var fact in facts.OrderByDescending(fact => fact.UpdatedAtUtc))
        {
            Facts.Add(fact);
        }
    }

    private void ResetFactDraft()
    {
        editingFact = null;
        IsEditingFact = false;
        SelectedFactCategory = CandidateFactCategory.Skill;
        FactContent = string.Empty;
        FactSourceText = string.Empty;
        FactConfidence = 0;
        FactIsConfirmed = false;
    }

    private async Task RunBusyAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "操作已取消。";
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
}
