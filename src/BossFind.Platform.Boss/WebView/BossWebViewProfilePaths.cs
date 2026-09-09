namespace BossFind.Platform.Boss.WebView;

public static class BossWebViewProfilePaths
{
    public static string GetBossProfileDirectory(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);

        return Path.Combine(localApplicationData, "BossFind", "WebView2", "BossProfile");
    }
}
