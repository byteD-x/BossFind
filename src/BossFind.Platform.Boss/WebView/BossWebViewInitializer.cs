using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace BossFind.Platform.Boss.WebView;

public sealed record WebView2InitializationResult(
    bool IsSuccess,
    string Message,
    BossWebViewEnvironment? Environment);

public static class BossWebViewInitializer
{
    public static async Task<WebView2InitializationResult> InitializeAsync(
        WebView2 webView,
        BossWebViewEnvironmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webView);

        var runtimeStatus = WebView2RuntimeProbe.GetStatus();
        if (!runtimeStatus.IsAvailable)
        {
            return new WebView2InitializationResult(
                false,
                "未检测到 Microsoft Edge WebView2 Evergreen Runtime。请安装或修复运行时后重试。",
                null);
        }

        try
        {
            var environment = await BossWebViewEnvironmentFactory.CreateAsync(options, cancellationToken);
            await webView.EnsureCoreWebView2Async(environment.Environment);
            return new WebView2InitializationResult(
                true,
                $"WebView2 已初始化，运行时版本：{runtimeStatus.Version}",
                environment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new WebView2InitializationResult(
                false,
                $"WebView2 初始化失败：{exception.Message}",
                null);
        }
    }
}
