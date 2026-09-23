using BossFind.App.ViewModels;
using BossFind.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;

namespace BossFind.App;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Routes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["dashboard"] = typeof(DashboardPage),
            ["browser"] = typeof(BossBrowserPage),
            ["profiles"] = typeof(CandidateProfilesPage),
            ["jobs"] = typeof(JobsPage),
            ["applications"] = typeof(ApplicationsPage),
            ["insights"] = typeof(InsightsPage),
            ["settings"] = typeof(SettingsPage)
        };

    private readonly ShellViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        ApplyNativeWindowChrome();
        viewModel = App.Services.GetRequiredService<ShellViewModel>();
        RootGrid.DataContext = viewModel;
        ContentFrame.Navigate(typeof(DashboardPage));
        RootNavigationView.SelectedItem = RootNavigationView.MenuItems.OfType<NavigationViewItem>().First(item => item.Tag?.ToString() == "dashboard");
    }

    // 原生窗口外观：内容延伸进标题栏，拖拽区由 AppTitleBar 承担；
    // Mica 背景材料仅 Windows 11 支持，Windows 10 自动使用默认窗口背景
    private void ApplyNativeWindowChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            SystemBackdrop = new MicaBackdrop();
        }
    }

    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var route = args.IsSettingsInvoked
            ? "settings"
            : (args.InvokedItemContainer as NavigationViewItem)?.Tag?.ToString();

        if (route is null || !Routes.TryGetValue(route, out var pageType))
        {
            return;
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
            viewModel.CurrentPageTitle = (args.InvokedItemContainer as NavigationViewItem)?.Content?.ToString()
                ?? "设置";
        }
    }

    public void Navigate(string route)
    {
        if (!Routes.TryGetValue(route, out var pageType))
        {
            return;
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        RootNavigationView.SelectedItem = RootNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .Concat(RootNavigationView.FooterMenuItems.OfType<NavigationViewItem>())
            .FirstOrDefault(item => item.Tag?.ToString() == route);
        viewModel.CurrentPageTitle = route switch
        {
            "browser" => "Boss 直聘浏览器",
            "profiles" => "候选人档案库",
            "jobs" => "岗位记录",
            "applications" => "投递与复核",
            "insights" => "岗位分析",
            "settings" => "设置",
            _ => "工作台"
        };
    }

    public void OpenPosting(string url)
    {
        Navigate("browser");
        if (ContentFrame.Content is BossBrowserPage browserPage)
        {
            browserPage.OpenUrl(url);
        }
    }
}
