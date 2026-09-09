using BossFind.App.ViewModels;
using BossFind.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App;

public sealed partial class MainWindow : Window
{
    private static readonly Dictionary<string, Type> Routes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["dashboard"] = typeof(DashboardPage),
            ["browser"] = typeof(BossBrowserPage),
            ["profiles"] = typeof(CandidateProfilesPage),
            ["settings"] = typeof(SettingsPage)
        };

    private readonly ShellViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<ShellViewModel>();
        ContentFrame.Navigate(typeof(DashboardPage));
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
}
