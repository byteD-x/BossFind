namespace BossFind.Platform.Boss.WebView;

public static class BossWebViewProfileCleaner
{
    public static bool Clear(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);

        var profilePath = BossWebViewProfilePaths.GetBossProfileDirectory(localApplicationData);
        if (!Directory.Exists(profilePath))
        {
            return false;
        }

        Directory.Delete(profilePath, recursive: true);
        return true;
    }
}
