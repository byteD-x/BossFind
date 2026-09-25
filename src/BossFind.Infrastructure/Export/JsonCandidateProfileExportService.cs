using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BossFind.Application.Profiles;
using BossFind.Domain.Entities;

namespace BossFind.Infrastructure.Export;

public sealed class JsonCandidateProfileExportService : ICandidateProfileExportService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static JsonCandidateProfileExportService()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly ICandidateProfileRepository repository;

    public JsonCandidateProfileExportService(ICandidateProfileRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task ExportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // ListProfilesAsync 已通过 Include 一次性加载事实，直接复用结果，避免每个档案再发起一次查询。
        var profiles = await repository.ListProfilesAsync(null, cancellationToken);
        var exportProfiles = new List<ExportProfile>(profiles.Count);
        foreach (var profile in profiles
                     .OrderByDescending(profile => profile.UpdatedAtUtc)
                     .ThenBy(profile => profile.Name, StringComparer.Ordinal)
                     .ThenBy(profile => profile.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            exportProfiles.Add(MapProfile(profile));
        }

        var document = new ExportDocument(exportProfiles);
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json, Utf8NoBom, cancellationToken);
    }

    private static ExportProfile MapProfile(CandidateProfile profile)
    {
        var facts = profile.Facts
            .OrderBy(fact => fact.Category)
            .ThenBy(fact => fact.Content, StringComparer.Ordinal)
            .ThenBy(fact => fact.Id)
            .Select(fact => new ExportFact(
                fact.Id,
                fact.ProfileId,
                fact.Category,
                fact.Content,
                fact.SourceText,
                fact.Confidence,
                fact.IsConfirmed,
                NormalizeUtc(fact.CreatedAtUtc),
                NormalizeUtc(fact.UpdatedAtUtc),
                fact.Version))
            .ToArray();

        return new ExportProfile(
            profile.Id,
            profile.Name,
            profile.Headline,
            profile.Location,
            profile.Email,
            profile.Phone,
            NormalizeUtc(profile.CreatedAtUtc),
            NormalizeUtc(profile.UpdatedAtUtc),
            profile.Version,
            facts);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private sealed record ExportDocument(IReadOnlyList<ExportProfile> Profiles);

    private sealed record ExportProfile(
        Guid Id,
        string Name,
        string Headline,
        string Location,
        string Email,
        string Phone,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        long Version,
        IReadOnlyList<ExportFact> Facts);

    private sealed record ExportFact(
        Guid Id,
        Guid ProfileId,
        Domain.Enums.CandidateFactCategory Category,
        string Content,
        string SourceText,
        decimal Confidence,
        bool IsConfirmed,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        long Version);
}
