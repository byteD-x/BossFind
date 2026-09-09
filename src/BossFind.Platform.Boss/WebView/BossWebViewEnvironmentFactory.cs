using System.Net;
using System.Net.Sockets;
using Microsoft.Web.WebView2.Core;

namespace BossFind.Platform.Boss.WebView;

public sealed record BossWebViewEnvironmentOptions(
    string? UserDataFolder = null,
    bool EnableRemoteDebugging = false);

public sealed record BossWebViewEnvironment(
    CoreWebView2Environment Environment,
    string UserDataFolder,
    Uri? CdpEndpoint);

public static class BossWebViewEnvironmentFactory
{
    public static async Task<BossWebViewEnvironment> CreateAsync(
        BossWebViewEnvironmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var userDataFolder = options.UserDataFolder
            ?? BossWebViewProfilePaths.GetBossProfileDirectory(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Directory.CreateDirectory(userDataFolder);

        Uri? cdpEndpoint = null;
        var environmentOptions = new CoreWebView2EnvironmentOptions();
        if (options.EnableRemoteDebugging)
        {
            var port = GetAvailableLoopbackPort();
            cdpEndpoint = new Uri($"http://127.0.0.1:{port}");
            environmentOptions.AdditionalBrowserArguments =
                $"--remote-debugging-address=127.0.0.1 --remote-debugging-port={port}";
        }

        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
            browserExecutableFolder: null,
            userDataFolder,
            environmentOptions);

        return new BossWebViewEnvironment(environment, userDataFolder, cdpEndpoint);
    }

    private static int GetAvailableLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
