using CommunityToolkit.Mvvm.ComponentModel;

namespace BossFind.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string CurrentPageTitle { get; set; } = "工作台";
}
