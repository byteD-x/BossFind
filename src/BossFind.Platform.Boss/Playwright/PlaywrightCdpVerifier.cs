using Microsoft.Playwright;

namespace BossFind.Platform.Boss.Playwright;

public sealed record CdpVerificationResult(bool IsConnected, int ContextCount, int PageCount);

public static class PlaywrightCdpVerifier
{
    public static async Task<CdpVerificationResult> VerifyAsync(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsLoopback)
        {
            throw new ArgumentException("CDP 端点只能使用本机回环地址。", nameof(endpoint));
        }

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(endpoint.AbsoluteUri);
        var contexts = browser.Contexts;
        var pageCount = contexts.Sum(context => context.Pages.Count);

        return new CdpVerificationResult(true, contexts.Count, pageCount);
    }
}
