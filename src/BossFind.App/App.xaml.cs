using BossFind.App.Hosting;
using BossFind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;

namespace BossFind.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private IHost? host;
    private Window? window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    public static IServiceProvider Services =>
        ((App)Current).host?.Services
        ?? throw new InvalidOperationException("应用服务尚未初始化。");

    public MainWindow? MainWindow => window as MainWindow;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            host = BossFindHost.Build();
            await host.StartAsync();

            await using var scope = host.Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();

            window = new MainWindow();
            window.Closed += OnWindowClosed;
            window.Activate();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "应用启动失败");
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BossFind",
                "Logs");
            window = new Window
            {
                Title = "BossFind 启动失败",
                Content = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = $"无法初始化 BossFind。请检查 WebView2 Runtime、应用数据目录和数据库迁移。日志目录：{logDirectory}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(32)
                }
            };
            window.Activate();
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        Log.Fatal(args.Exception, "发生未处理的 UI 异常");
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
            host = null;
        }
    }
}
