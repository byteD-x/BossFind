using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace BossFind.Platform.Boss.Parsing;

public static class BossJobSearchResultParser
{
    private static readonly string[] CardSelectors =
    [
        "[data-testid='job-card']",
        ".job-card-wrapper",
        ".job-card",
        "li.job-card"
    ];

    public static IReadOnlyList<BossJobSearchResult> Parse(string html, string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = new HtmlParser().ParseDocument(html);
        var cards = CardSelectors
            .SelectMany(selector => document.QuerySelectorAll(selector))
            .Distinct()
            .ToArray();
        if (cards.Length == 0)
        {
            return [];
        }

        Uri? baseUri = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUri)
            ? parsedBaseUri
            : null;
        var results = new List<BossJobSearchResult>(cards.Length);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            var title = GetFirstText(card, "[data-testid='job-title']", ".job-name", ".job-title", "h3", "h4");
            var url = ResolveUrl(card.QuerySelector("a[href]")?.GetAttribute("href"), baseUri);
            var externalId = GetAttribute(card, "data-job-id", "data-jobid");
            externalId = string.IsNullOrWhiteSpace(externalId)
                ? ExtractExternalId(url)
                : externalId.Trim();
            if (title.Length == 0 || (url.Length == 0 && externalId.Length == 0))
            {
                continue;
            }

            var dedupeKey = externalId.Length > 0 ? externalId : url;
            if (!seenKeys.Add(dedupeKey))
            {
                continue;
            }

            var requirementText = GetFirstText(card, ".job-limit", ".job-requirement", "[data-testid='job-requirement']");
            var experience = GetFirstText(card, "[data-testid='job-experience']", ".job-experience", ".experience");
            var education = GetFirstText(card, "[data-testid='job-education']", ".job-education", ".education");
            if (experience.Length == 0) experience = ExtractExperience(requirementText);
            if (education.Length == 0) education = ExtractEducation(requirementText);

            results.Add(new BossJobSearchResult(
                title,
                GetFirstText(card, "[data-testid='company-name']", ".company-name", ".company-info .name"),
                NormalizeCity(GetFirstText(card, "[data-testid='job-city']", ".job-area", ".job-location", ".location")),
                GetFirstText(card, "[data-testid='job-salary']", ".job-salary", ".salary"),
                experience,
                education,
                GetTags(card),
                url,
                externalId));
        }

        return results;
    }

    private static string[] GetTags(IElement card)
    {
        return card.QuerySelectorAll(
                "[data-testid='job-tag'], .job-tags .tag, .job-tags li, .tag-list .tag, .tag-list li")
            .Select(GetText)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetFirstText(IParentNode root, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var value = GetText(root.QuerySelector(selector));
            if (value.Length > 0)
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string GetText(IElement? element)
    {
        return element is null
            ? string.Empty
            : string.Join(' ', element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetAttribute(IElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = element.GetAttribute(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ResolveUrl(string? href, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(href.Trim(), UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }

        return baseUri is not null && Uri.TryCreate(baseUri, href.Trim(), out var resolved)
            ? resolved.AbsoluteUri
            : string.Empty;
    }

    private static string ExtractExternalId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var detailIndex = Array.FindIndex(segments, segment =>
            segment.Equals("job_detail", StringComparison.OrdinalIgnoreCase));
        if (detailIndex < 0 || detailIndex + 1 >= segments.Length)
        {
            return string.Empty;
        }

        var externalId = segments[detailIndex + 1];
        return externalId.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? externalId[..^5]
            : externalId;
    }

    private static string NormalizeCity(string value)
    {
        var separatorIndex = value.IndexOfAny(['·', '/', ' ']);
        return separatorIndex > 0 ? value[..separatorIndex] : value;
    }

    private static string ExtractExperience(string value)
    {
        if (value.Contains("经验不限", StringComparison.OrdinalIgnoreCase)
            || value.Contains("不限经验", StringComparison.OrdinalIgnoreCase))
        {
            return "经验不限";
        }

        var yearIndex = value.IndexOf('年');
        return yearIndex >= 0 ? value[..(yearIndex + 1)] : string.Empty;
    }

    private static string ExtractEducation(string value)
    {
        foreach (var education in new[] { "博士", "硕士", "本科", "大专", "学历不限" })
        {
            if (value.Contains(education, StringComparison.OrdinalIgnoreCase))
            {
                return education;
            }
        }

        return string.Empty;
    }
}
