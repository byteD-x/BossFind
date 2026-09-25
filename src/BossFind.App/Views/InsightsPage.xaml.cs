using BossFind.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BossFind.App.Views;

public sealed partial class InsightsPage : Page
{
    private readonly InsightsViewModel viewModel;
    private bool isCompactSelector;
    private bool isCompactMatch;

    public InsightsPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<InsightsViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateResponsiveLayout();
        await viewModel.LoadAsync();
        if (viewModel.Summary.Length == 0)
        {
            await viewModel.AnalyzeAsync();
        }
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs args) => await viewModel.AnalyzeAsync();

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        var compactSelector = ActualWidth < 860;
        if (compactSelector != isCompactSelector || SelectorGrid.ColumnDefinitions.Count == 0)
        {
            isCompactSelector = compactSelector;
            SelectorGrid.ColumnDefinitions.Clear();
            SelectorGrid.RowDefinitions.Clear();
            if (compactSelector)
            {
                SelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SelectorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SelectorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (SelectorGrid.Children[0] is FrameworkElement first
                    && SelectorGrid.Children[1] is FrameworkElement second
                    && SelectorGrid.Children[2] is FrameworkElement action)
                {
                    Grid.SetColumn(first, 0);
                    Grid.SetRow(first, 0);
                    Grid.SetColumn(second, 1);
                    Grid.SetRow(second, 0);
                    Grid.SetColumn(action, 1);
                    Grid.SetRow(action, 1);
                }
            }
            else
            {
                SelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                SelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SelectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                SelectorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (var index = 0; index < SelectorGrid.Children.Count; index++)
                {
                    if (SelectorGrid.Children[index] is FrameworkElement child)
                    {
                        Grid.SetColumn(child, index);
                        Grid.SetRow(child, 0);
                    }
                }
            }
        }

        var compactMatch = ActualWidth < 760;
        if (compactMatch == isCompactMatch && MatchGrid.ColumnDefinitions.Count > 0)
        {
            return;
        }

        isCompactMatch = compactMatch;
        MatchGrid.ColumnDefinitions.Clear();
        MatchGrid.RowDefinitions.Clear();
        if (compactMatch)
        {
            MatchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            MatchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            MatchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(MatchedPanel, 0);
            Grid.SetRow(MatchedPanel, 0);
            Grid.SetColumn(MissingPanel, 0);
            Grid.SetRow(MissingPanel, 1);
        }
        else
        {
            MatchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            MatchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            MatchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(MatchedPanel, 0);
            Grid.SetRow(MatchedPanel, 0);
            Grid.SetColumn(MissingPanel, 1);
            Grid.SetRow(MissingPanel, 0);
        }
    }
}
