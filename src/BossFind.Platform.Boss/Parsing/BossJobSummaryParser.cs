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

        var title = GetFirstText(jobDetail, "[data-testid='job-title']", "h1.name", ".job-primary h1", ".job-name", ".job-title");
        var company = GetFirstText(jobDetail, "[data-testid='company-name']", ".company-name", ".company-info .name", ".sider-company .name", ".job-primary .company-name");
        var city = GetFirstText(jobDetail, "[data-testid='job-city']", ".location-address", ".job-location", ".location", ".job-primary .location");
        var salary = GetFirstText(jobDetail, "[data-testid='job-salary']", ".job-salary", ".salary", ".job-primary .salary");
        if (salary.Length == 0)
        {
            var jobName = GetFirstText(jobDetail, ".job-primary .job-name");
            if (LooksLikeSalary(jobName)) salary = jobName;
        }
        var experience = GetFirstText(jobDetail, "[data-testid='job-experience']", ".job-experience", ".experience", ".job-primary .tag-list li:nth-child(1)");
        var education = GetFirstText(jobDetail, "[data-testid='job-education']", ".job-education", ".education", ".job-primary .tag-list li:nth-child(2)");
        var description = GetFirstText(jobDetail, "[data-testid='job-description']", ".job-description", ".job-sec-text", ".job-detail", ".job-detail-section");
        var benefits = jobDetail
            .QuerySelectorAll("[data-testid='job-benefit'], .benefit, .福利, .welfare")
            .Select(GetText)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var tags = jobDetail
            .QuerySelectorAll(".tag")
            .Select(GetText)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var url = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href") ?? string.Empty;
        var externalId = (jobDetail as IElement)?.GetAttribute("data-job-id")
            ?? (jobDetail.QuerySelector("[data-job-id]") as IElement)?.GetAttribute("data-job-id")
            ?? string.Empty;
        return new BossJobSummary(title, company, city, tags, salary, experience, education, benefits, description, url, externalId);
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

    private static bool LooksLikeSalary(string value)
    {
        return value.Any(char.IsDigit)
            && (value.Any(character => char.ToUpperInvariant(character) is 'K' or 'W' or '万' or '元')
                || value.Contains('薪')
                || value.Contains('天'));
    }
}
