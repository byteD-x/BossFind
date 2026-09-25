using System.Text;
using BossFind.Domain.Entities;
using BossFind.Domain.Enums;

namespace BossFind.Application.Profiles;

public sealed record ResumeChunk(
    string FieldKey,
    string Title,
    string Content);

public static class ResumeChunkExtractor
{
    private static readonly Dictionary<string, string> SectionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["基本信息"] = "basic",
            ["个人信息"] = "basic",
            ["求职意向"] = "intention",
            ["职业目标"] = "intention",
            ["教育背景"] = "education",
            ["教育经历"] = "education",
            ["工作经历"] = "experience",
            ["工作经验"] = "experience",
            ["实习经历"] = "experience",
            ["项目经历"] = "project",
            ["项目经验"] = "project",
            ["专业技能"] = "skills",
            ["技能特长"] = "skills",
            ["技能"] = "skills",
            ["证书与荣誉"] = "skills",
            ["证书荣誉"] = "skills",
            ["自我评价"] = "summary",
            ["个人简介"] = "summary",
            ["自我介绍"] = "summary"
        };

    public static IReadOnlyList<ResumeChunk> Extract(
        CandidateProfile profile,
        string? sourceText = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var labeledValues = ParseLabeledValues(sourceText);
        var sections = ParseSections(sourceText);
        var chunks = new List<ResumeChunk>();

        AddChunk(chunks, "name", "姓名", GetLabeledValue(labeledValues, "姓名", "名字") ?? profile.Name);
        AddChunk(chunks, "phone", "手机号", GetLabeledValue(labeledValues, "手机号", "手机", "电话") ?? profile.Phone);
        AddChunk(chunks, "email", "邮箱", GetLabeledValue(labeledValues, "邮箱", "电子邮箱") ?? profile.Email);
        AddChunk(chunks, "location", "所在地", GetLabeledValue(labeledValues, "所在地", "城市", "地址") ?? profile.Location);
        AddChunk(chunks, "intention", "求职意向", GetLabeledValue(labeledValues, "求职意向", "期望职位", "目标岗位")
            ?? (string.IsNullOrWhiteSpace(profile.Headline) ? sections.GetValueOrDefault("intention") : profile.Headline));
        AddChunk(chunks, "education", "教育背景", GetLabeledValue(labeledValues, "教育背景", "教育经历")
            ?? sections.GetValueOrDefault("education")
            ?? JoinFacts(profile, CandidateFactCategory.Education));
        AddChunk(chunks, "experience", "工作经历", GetLabeledValue(labeledValues, "工作经历", "工作经验")
            ?? sections.GetValueOrDefault("experience")
            ?? JoinFacts(profile, CandidateFactCategory.Experience));
        AddChunk(chunks, "project", "项目经历", GetLabeledValue(labeledValues, "项目经历", "项目经验")
            ?? sections.GetValueOrDefault("project")
            ?? JoinFacts(profile, CandidateFactCategory.Project));
        AddChunk(chunks, "skills", "技能与证书", GetLabeledValue(labeledValues, "技能", "专业技能", "技能特长", "证书")
            ?? sections.GetValueOrDefault("skills")
            ?? JoinFacts(profile, CandidateFactCategory.Skill, CandidateFactCategory.Certificate, CandidateFactCategory.Achievement));
        AddChunk(chunks, "summary", "自我介绍", GetLabeledValue(labeledValues, "自我评价", "个人简介", "自我介绍")
            ?? sections.GetValueOrDefault("summary")
            ?? JoinFacts(profile, CandidateFactCategory.Preference));

        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(sourceText))
        {
            chunks.Add(new ResumeChunk("summary", "简历原文", Normalize(sourceText)));
        }

        return chunks;
    }

    private static void AddChunk(
        List<ResumeChunk> chunks,
        string fieldKey,
        string title,
        string? content)
    {
        var normalized = Normalize(content);
        if (normalized.Length > 0)
        {
            chunks.Add(new ResumeChunk(fieldKey, title, normalized));
        }
    }

    private static string? JoinFacts(
        CandidateProfile profile,
        params CandidateFactCategory[] categories)
    {
        var values = profile.Facts
            .Where(fact => categories.Contains(fact.Category))
            .OrderByDescending(fact => fact.IsConfirmed)
            .ThenByDescending(fact => fact.UpdatedAtUtc)
            .Select(fact => Normalize(fact.Content))
            .Where(content => content.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static Dictionary<string, string> ParseLabeledValues(string? sourceText)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in SplitLines(sourceText))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOfAny(['：', ':', '|']);
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            var key = NormalizeHeading(line[..separatorIndex]);
            var value = Normalize(line[(separatorIndex + 1)..]);
            if (key.Length > 0 && value.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static Dictionary<string, string> ParseSections(string? sourceText)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var content = new StringBuilder();

        foreach (var rawLine in SplitLines(sourceText))
        {
            var heading = NormalizeHeading(rawLine);
            if (SectionAliases.TryGetValue(heading, out var section))
            {
                SaveSection(sections, currentSection, content);
                currentSection = section;
                content.Clear();
                continue;
            }

            if (currentSection is not null)
            {
                if (content.Length > 0)
                {
                    content.AppendLine();
                }

                content.Append(rawLine.Trim());
            }
        }

        SaveSection(sections, currentSection, content);
        return sections;
    }

    private static void SaveSection(
        Dictionary<string, string> sections,
        string? section,
        StringBuilder content)
    {
        if (section is null)
        {
            return;
        }

        var normalized = Normalize(content.ToString());
        if (normalized.Length > 0)
        {
            sections[section] = normalized;
        }
    }

    private static string[] SplitLines(string? sourceText)
    {
        return (sourceText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None);
    }

    private static string? GetLabeledValue(
        Dictionary<string, string> values,
        params string[] labels)
    {
        foreach (var label in labels)
        {
            if (values.TryGetValue(label, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeHeading(string value)
    {
        return value.Trim().Trim(' ', '\t', ':', '：', '-', '—', '【', '】', '[', ']', '#');
    }

    private static string Normalize(string? value)
    {
        return string.Join(
            Environment.NewLine,
            (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
