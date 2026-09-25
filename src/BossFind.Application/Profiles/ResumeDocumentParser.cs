using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BossFind.Domain.Entities;

namespace BossFind.Application.Profiles;

public static class ResumeDocumentParser
{
    private static readonly Regex PdfTextRegex = new(@"\((?<text>(?:\\.|[^\\)])*)\)\s*T[Jj]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<string> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".docx" => await ParseDocxAsync(filePath, cancellationToken),
            ".pdf" => await ParsePdfAsync(filePath, cancellationToken),
            ".txt" or ".md" or ".rtf" => await File.ReadAllTextAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException("支持 PDF、DOCX、TXT、MD 和 RTF 简历文件。")
        };
    }

    private static async Task<string> ParseDocxAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("DOCX 文件缺少正文内容。");
        await using var entryStream = entry.Open();
        var document = await XDocument.LoadAsync(entryStream, LoadOptions.None, cancellationToken);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join(
            Environment.NewLine,
            document.Descendants(word + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(text => text.Value)).Trim())
                .Where(line => line.Length > 0));
    }

    private static async Task<string> ParsePdfAsync(string filePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var raw = Encoding.Latin1.GetString(bytes);
        var matches = PdfTextRegex.Matches(raw);
        var text = string.Join(
            Environment.NewLine,
            matches.Select(match => UnescapePdfText(match.Groups["text"].Value)).Where(value => value.Length > 0));
        if (text.Length > 0)
        {
            return text;
        }

        throw new InvalidDataException("未能从 PDF 提取文本，请先复制文字后导入。扫描版 PDF 暂不支持 OCR。");
    }

    private static string UnescapePdfText(string value)
    {
        return value
            .Replace(@"\(", "(", StringComparison.Ordinal)
            .Replace(@"\)", ")", StringComparison.Ordinal)
            .Replace(@"\\", "\\", StringComparison.Ordinal)
            .Trim();
    }
}

public sealed class ResumeDocumentImportService(CandidateProfileService profileService)
{
    public async Task<CandidateProfile> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var text = await ResumeDocumentParser.ParseAsync(filePath, cancellationToken);
        var name = Path.GetFileNameWithoutExtension(filePath).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "导入的求职者";
        }

        var profile = await profileService.CreateAsync(name, "待补充职位方向", cancellationToken: cancellationToken);
        var chunks = ResumeChunkExtractor.Extract(profile, text);
        foreach (var chunk in chunks)
        {
            var category = chunk.FieldKey switch
            {
                "education" => Domain.Enums.CandidateFactCategory.Education,
                "experience" => Domain.Enums.CandidateFactCategory.Experience,
                "project" => Domain.Enums.CandidateFactCategory.Project,
                "skills" => Domain.Enums.CandidateFactCategory.Skill,
                "summary" => Domain.Enums.CandidateFactCategory.Achievement,
                _ => Domain.Enums.CandidateFactCategory.Preference
            };
            await profileService.AddFactAsync(
                profile.Id,
                category,
                chunk.Content,
                $"导入文件：{Path.GetFileName(filePath)}",
                confidence: 0.6m,
                cancellationToken: cancellationToken);
        }

        return profile;
    }
}
