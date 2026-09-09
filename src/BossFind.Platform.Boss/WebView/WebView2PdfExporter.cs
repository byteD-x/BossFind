using Microsoft.Web.WebView2.Core;

namespace BossFind.Platform.Boss.WebView;

public static class WebView2PdfExporter
{
    public static async Task<bool> ExportAsync(CoreWebView2 webView, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(webView);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!Path.IsPathFullyQualified(outputPath))
        {
            throw new ArgumentException("PDF 输出路径必须是绝对路径。", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("PDF 输出路径不包含目录。", nameof(outputPath));
        Directory.CreateDirectory(directory);

        return await webView.PrintToPdfAsync(outputPath, null);
    }
}
