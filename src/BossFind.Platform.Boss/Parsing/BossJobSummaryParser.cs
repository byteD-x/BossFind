using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace BossFind.Platform.Boss.Parsing;

public static class BossJobSummaryParser
{
    public static BossJobSummary Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = new HtmlParser().ParseDocument(html);
        IParentNode jobDetail = document.QuerySelector("[data-testid='job-detail']") as IParentNode
            ?? document;

        var title = GetText(jobDetail.QuerySelector("[data-testid='job-title']"));
        var company = GetText(jobDetail.QuerySelector("[data-testid='company-name']"));
        var city = GetText(jobDetail.QuerySelector("[data-testid='job-city']"));
        var tags = jobDetail
            .QuerySelectorAll(".tag")
            .Select(GetText)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new BossJobSummary(title, company, city, tags);
    }

    private static string GetText(IElement? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
