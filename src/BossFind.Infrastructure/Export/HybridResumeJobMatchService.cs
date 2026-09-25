using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BossFind.Application.Matching;
using BossFind.Domain.Entities;

namespace BossFind.Infrastructure.Export;

public sealed class HybridResumeJobMatchService : IResumeJobMatchService, IDisposable
{
    private static readonly TimeSpan ExternalRequestTimeout = TimeSpan.FromSeconds(15);
    private readonly LocalResumeJobMatchService local = new();
    private readonly HttpClient httpClient = new() { Timeout = ExternalRequestTimeout };
    private readonly string? endpoint = Environment.GetEnvironmentVariable("BOSSFIND_LLM_ENDPOINT");
    private readonly string? llmAuthorization = Environment.GetEnvironmentVariable("BOSSFIND_LLM_API_KEY");
    private readonly string model = Environment.GetEnvironmentVariable("BOSSFIND_LLM_MODEL") ?? "gpt-4o-mini";

    public async Task<JobMatchResult> MatchAsync(
        CandidateProfile profile,
        JobRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(llmAuthorization))
        {
            return await local.MatchAsync(profile, requirement, cancellationToken);
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            return await local.MatchAsync(profile, requirement, cancellationToken);
        }

        try
        {
            var payload = new
            {
                model,
                temperature = 0.1,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = "你是求职匹配助手。只返回 JSON：score(0到1小数)、matchedSkills(字符串数组)、missingSkills(字符串数组)、explanation(简体中文字符串)。" },
                    new { role = "user", content = BuildPrompt(profile, requirement) }
                }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", llmAuthorization);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (responseJson.Length > 1_000_000)
            {
                throw new InvalidDataException("外部匹配服务响应过大。");
            }

            return ParseResponse(responseJson);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await local.MatchAsync(profile, requirement, cancellationToken);
        }
    }

    private static string BuildPrompt(CandidateProfile profile, JobRequirement requirement)
    {
        var facts = string.Join("\n", profile.Facts.Select(fact => $"- {fact.Content}"));
        return $"岗位：{requirement.Title}\n公司：{requirement.Company}\n城市：{requirement.City}\n技能：{string.Join("、", requirement.RequiredSkills ?? [])}\n简历：{profile.Name}｜{profile.Headline}｜{profile.Location}\n经历事实：\n{facts}";
    }

    private static JobMatchResult ParseResponse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        content = content.Trim().Trim('`');
        if (content.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            content = content[4..].Trim();
        }

        using var result = JsonDocument.Parse(content);
        var root = result.RootElement;
        var score = Math.Clamp(root.GetProperty("score").GetDecimal(), 0m, 1m);
        var matched = root.GetProperty("matchedSkills").EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray();
        var missing = root.GetProperty("missingSkills").EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray();
        var explanation = root.GetProperty("explanation").GetString() ?? "已完成语义匹配。";
        return new JobMatchResult(score, matched, missing, explanation);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
