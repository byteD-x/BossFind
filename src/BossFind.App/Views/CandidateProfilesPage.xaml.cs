using BossFind.App.ViewModels;
using BossFind.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace BossFind.App.Views;

public sealed partial class CandidateProfilesPage : Page
{
    private readonly CandidateProfilesViewModel viewModel;
    private bool hasLoaded;

    public CandidateProfilesPage()
    {
        InitializeComponent();
        viewModel = App.Services.GetRequiredService<CandidateProfilesViewModel>();
        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        await viewModel.LoadAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs args)
    {
        viewModel.BeginNew();
        ProfilesList.SelectedItem = null;
    }

    private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.AddedItems.Count == 0 || args.AddedItems[0] is not CandidateProfile profile)
        {
            return;
        }

        await viewModel.SelectProfileAsync(profile);
    }

    private async void OnSearchClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SearchAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
    }

    private async void OnClearSearchClick(object sender, RoutedEventArgs args)
    {
        await viewModel.ClearSearchAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
        SearchTextBox.Focus(FocusState.Programmatic);
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter)
        {
            return;
        }

        args.Handled = true;
        await viewModel.SearchAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
    }

    private async void OnSaveProfileClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SaveAsync();
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
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
        ProfilesList.SelectedItem = viewModel.SelectedProfile;
    }

    private async void OnSaveFactClick(object sender, RoutedEventArgs args)
    {
        await viewModel.SaveFactAsync();
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
            }
        }
    }
}
