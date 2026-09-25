using System.Collections.ObjectModel;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class OnlineResumeViewModel(CandidateProfileService profileService) : ObservableObject
{
    private static readonly (string Key, string Title)[] SectionOrder =
    [
        ("basic", "基本信息"),
        ("skills", "技能与证书"),
        ("experience", "工作经历"),
        ("project", "项目经历"),
        ("education", "教育背景"),
        ("summary", "自我介绍"),
    ];

    public ObservableCollection<CandidateProfile> Profiles { get; } = [];

    public ObservableCollection<ResumeDisplaySection> Sections { get; } = [];

    [ObservableProperty]
    public partial CandidateProfile? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "请选择简历";

    public bool HasSections => Sections.Count > 0;

    public bool ShowEmptyState => !HasSections;

    partial void OnSelectedProfileChanged(CandidateProfile? value) => Extract();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var profiles = await profileService.ListAsync(cancellationToken: cancellationToken);
            Profiles.Clear();
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            SelectedProfile ??= Profiles.FirstOrDefault();
            if (SelectedProfile is null)
            {
                Status = "暂无简历";
                Sections.Clear();
                OnPropertyChanged(nameof(HasSections));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "已取消";
        }
        catch (Exception exception)
        {
            Status = $"加载简历失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Extract()
    {
        Sections.Clear();
        if (SelectedProfile is null)
        {
            Status = "请选择简历";
            OnPropertyChanged(nameof(HasSections));
            OnPropertyChanged(nameof(ShowEmptyState));
            return;
        }

        var chunks = ResumeChunkExtractor.Extract(SelectedProfile, SourceText);
        foreach (var (key, title) in SectionOrder)
        {
            var matchingChunks = chunks.Where(chunk => GetSectionKey(chunk.FieldKey) == key).ToArray();
            if (matchingChunks.Length == 0)
            {
                continue;
            }

            var items = matchingChunks
                .SelectMany(chunk => SplitItems(chunk, key))
                .ToArray();
            if (items.Length > 0)
            {
                Sections.Add(new ResumeDisplaySection(title, items));
            }
        }

        var itemCount = Sections.Sum(section => section.Items.Count);
        Status = itemCount == 0
            ? "暂无内容"
            : $"{itemCount} 条内容";
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private static string GetSectionKey(string fieldKey) => fieldKey switch
    {
        "name" or "phone" or "email" or "location" or "intention" => "basic",
        _ => fieldKey
    };

    private static IEnumerable<ResumeDisplayItem> SplitItems(ResumeChunk chunk, string sectionKey)
    {
        if (sectionKey == "basic" || sectionKey == "summary")
        {
            yield return new ResumeDisplayItem(chunk.Title, chunk.Content, chunk.FieldKey);
            yield break;
        }

        var values = sectionKey == "skills"
            ? chunk.Content.Split(['、', '，', ',', '\n', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : chunk.Content.Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var index = 0;
        foreach (var value in values)
        {
            index++;
            var title = sectionKey == "skills"
                ? $"技能 {index}"
                : $"{chunk.Title} {index}";
            yield return new ResumeDisplayItem(title, value, chunk.FieldKey);
        }
    }
}

public sealed record ResumeDisplaySection(
    string Title,
    IReadOnlyList<ResumeDisplayItem> Items)
{
    public string CountText => $"{Items.Count} 条";
}

public sealed record ResumeDisplayItem(
    string Title,
    string Content,
    string FieldKey);
