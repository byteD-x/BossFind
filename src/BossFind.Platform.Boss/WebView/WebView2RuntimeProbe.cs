using Microsoft.Web.WebView2.Core;

namespace BossFind.Platform.Boss.WebView;

public sealed record WebView2RuntimeStatus(bool IsAvailable, string? Version, string? Error);

public static class WebView2RuntimeProbe
{
    public static WebView2RuntimeStatus GetStatus()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return new WebView2RuntimeStatus(true, version, null);
        }
        catch (Exception exception)
        {
            return new WebView2RuntimeStatus(false, null, exception.Message);
        }
    }
}
