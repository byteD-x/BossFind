using BossFind.App.Services;
using BossFind.App.ViewModels;
using BossFind.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Serilog;
using Windows.Graphics;

namespace BossFind.App;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Routes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["dashboard"] = typeof(DashboardPage),
            ["browser"] = typeof(BossBrowserPage),
            ["profiles"] = typeof(CandidateProfilesPage),
            ["resume"] = typeof(OnlineResumePage),
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
        App.GlobalToasts.NotificationRequested += OnToastNotificationRequested;
        Closed += OnClosed;
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
        AppWindow.Resize(new SizeInt32(1440, 900));
        ApplyWindowIcon();
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            SystemBackdrop = new MicaBackdrop();
        }
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BossFindIcon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            AppWindow.SetIcon(iconPath);
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "设置窗口图标失败");
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

        TryNavigate(route, updateSelection: false);
    }

    private void OnNewProfileAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Navigate("profiles");
        if (ContentFrame.Content is CandidateProfilesPage profilesPage)
        {
            profilesPage.StartNewProfile();
        }
    }

    private void OnSubmitAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ContentFrame.Content is BossBrowserPage browserPage)
        {
            browserPage.SubmitCurrentApplication();
        }
    }

    private void OnToastNotificationRequested(object? sender, ToastNotificationEventArgs args)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            _ = DispatcherQueue.TryEnqueue(() => RenderToast(args));
            return;
        }

        RenderToast(args);
    }

    private void RenderToast(ToastNotificationEventArgs notification)
    {
        var existing = ToastHost.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(element => element.Tag is Guid id && id == notification.Id);
        if (notification.IsDismissal)
        {
            if (existing is not null)
            {
                ToastHost.Children.Remove(existing);
            }

            return;
        }

        if (existing is not null)
        {
            ToastHost.Children.Remove(existing);
        }

        var toast = new Border
        {
            Tag = notification.Id,
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AppToastBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9, 12, 9),
            MaxWidth = 360,
            Opacity = 0.98,
            RenderTransform = new TranslateTransform { X = 28 }
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new FontIcon
        {
            Glyph = notification.IconGlyph,
            FontSize = 15,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        });
        row.Children.Add(new TextBlock
        {
            Text = notification.Message,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 310
        });
        toast.Child = row;
        ToastHost.Children.Add(toast);

        var animation = new DoubleAnimation
        {
            From = 28,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, toast.RenderTransform);
        Storyboard.SetTargetProperty(animation, "X");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    public void Navigate(string route)
    {
        TryNavigate(route, updateSelection: true);
    }

    private bool TryNavigate(string route, bool updateSelection)
    {
        if (!Routes.TryGetValue(route, out var pageType))
        {
            return false;
        }

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            try
            {
                if (!ContentFrame.Navigate(pageType))
                {
                    Log.Warning("页面导航未完成：{Route}", route);
                    return false;
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception, "页面导航失败：{Route}", route);
                return false;
            }
        }

        if (updateSelection)
        {
            RootNavigationView.SelectedItem = RootNavigationView.MenuItems
                .OfType<NavigationViewItem>()
                .Concat(RootNavigationView.FooterMenuItems.OfType<NavigationViewItem>())
                .FirstOrDefault(item => item.Tag?.ToString() == route);
        }

        viewModel.CurrentPageTitle = route switch
        {
            "browser" => "Boss 直聘浏览器",
            "profiles" => "求职者简历",
            "resume" => "在线简历",
            "jobs" => "岗位记录",
            "applications" => "投递与复核",
            "insights" => "岗位分析",
            "settings" => "设置",
            _ => "工作台"
        };
        return true;
    }

    public void OpenPosting(string url)
    {
        Navigate("browser");
        if (ContentFrame.Content is BossBrowserPage browserPage)
        {
            browserPage.OpenUrl(url);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        App.GlobalToasts.NotificationRequested -= OnToastNotificationRequested;
        Closed -= OnClosed;
    }
}
