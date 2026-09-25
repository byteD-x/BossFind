using BossFind.App.ViewModels;
using BossFind.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace BossFind.App.Views;

public sealed partial class CandidateProfilesPage : Page
{
    private readonly CandidateProfilesViewModel viewModel;
    private bool hasLoaded;
    private bool isCompactLayout;
    private bool isCreatingProfile;

    public CandidateProfilesPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<CandidateProfilesViewModel>();
        DataContext = viewModel;
    }

    public void StartNewProfile()
    {
        isCreatingProfile = true;
        viewModel.BeginNew();
        ProfilesList.SelectedItem = null;
        EditorExpander.IsExpanded = true;
        UpdateEmptyState();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        UpdateResponsiveLayout();
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        isCreatingProfile = false;
        ProfilesLoadingState.Visibility = Visibility.Visible;
        ProfilesList.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Collapsed;
        ProfileDetailsEmptyState.Visibility = Visibility.Collapsed;
        await viewModel.LoadAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 900;
        if (compact == isCompactLayout && PageLayout.RowDefinitions.Count > 0)
        {
            return;
        }

        isCompactLayout = compact;
        PageLayout.ColumnDefinitions.Clear();
        PageLayout.RowDefinitions.Clear();
        PageLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (compact)
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(DetailsPanel, 0);
            Grid.SetRow(DetailsPanel, 2);
            Grid.SetColumn(ProfileDetailsEmptyState, 0);
            Grid.SetRow(ProfileDetailsEmptyState, 2);
        }
        else
        {
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
            PageLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PageLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(ListPanel, 0);
            Grid.SetRow(ListPanel, 1);
            Grid.SetColumn(DetailsPanel, 1);
            Grid.SetRow(DetailsPanel, 1);
            Grid.SetColumn(ProfileDetailsEmptyState, 1);
            Grid.SetRow(ProfileDetailsEmptyState, 1);
        }
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs args)
    {
        StartNewProfile();
    }

    private async void OnImportDocumentClick(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".docx");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".rtf");
        var windowHandle = WindowNative.GetWindowHandle(((BossFind.App.App)Microsoft.UI.Xaml.Application.Current).MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        await viewModel.ImportDocumentAsync(file.Path);
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
        App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count == 0 || args.AddedItems[0] is not CandidateProfile profile)
        {
            return;
        }

        isCreatingProfile = false;
        await viewModel.SelectProfileAsync(profile);
        UpdateEmptyState();
    }

    private async void OnSearchClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SearchAsync();
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
    }

    private async void OnClearSearchClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ClearSearchAsync();
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        SearchTextBox.Focus(FocusState.Programmatic);
        UpdateEmptyState();
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter)
        {
            return;
        }

        args.Handled = true;
        await viewModel.SearchAsync();
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
    }

    private async void OnSaveProfileClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SaveAsync();
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
        App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs args)
    {
        if (!viewModel.CanDeleteProfile)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"删除 {viewModel.Name} 的档案？",
            Content = "档案中的经历和技能记录也会删除。已有投递记录会保留，但不再关联此档案。",
            PrimaryButtonText = "删除档案",
            CloseButtonText = "返回",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await viewModel.DeleteAsync();
        isCreatingProfile = false;
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var isLoading = viewModel.IsLoading;
        var hasDetails = viewModel.SelectedProfile is not null || isCreatingProfile;
        ProfilesLoadingState.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        ProfilesList.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        ProfilesEmptyState.Visibility = !isLoading && viewModel.Profiles.Count == 0 && !isCreatingProfile
            ? Visibility.Visible
            : Visibility.Collapsed;
        DetailsPanel.Visibility = !isLoading && hasDetails ? Visibility.Visible : Visibility.Collapsed;
        ProfileDetailsEmptyState.Visibility = !isLoading && !hasDetails
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void OnSaveFactClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SaveFactAsync();
        App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
    }

    private void OnCancelFactClick(object sender, RoutedEventArgs args)
    {
        viewModel.BeginFactEdit(null);
    }

    private void OnEditFactClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CandidateFact fact })
        {
            viewModel.BeginFactEdit(fact);
        }
    }

    private async void OnDeleteFactClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: CandidateFact fact })
        {
            var dialog = new ContentDialog
            {
                Title = "删除这条事实？",
                Content = fact.Content,
                PrimaryButtonText = "删除",
                CloseButtonText = "返回",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await viewModel.DeleteFactAsync(fact);
                App.GlobalToasts.ShowInfo(viewModel.StatusMessage);
            }
        }
    }
}
