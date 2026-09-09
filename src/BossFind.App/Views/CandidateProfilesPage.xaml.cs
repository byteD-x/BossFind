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
            await viewModel.DeleteFactAsync(fact);
        }
    }
}
