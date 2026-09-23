using BossFind.App.ViewModels;
using BossFind.Application.Profiles;
using BossFind.Infrastructure;
using BossFind.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BossFind.App.Hosting;

public static class BossFindHost
{
    public static IHost Build()
    {
        var applicationDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BossFind");
        var databaseDirectory = Path.Combine(applicationDataDirectory, "Database");
        var logDirectory = Path.Combine(applicationDataDirectory, "Logs");
        Directory.CreateDirectory(databaseDirectory);

        return Host.CreateDefaultBuilder()
            .UseBossFindLogging(logDirectory)
            .ConfigureServices((_, services) =>
            {
                services.AddBossFindInfrastructure(Path.Combine(databaseDirectory, "bossfind.db"));
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<BossBrowserViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<CandidateProfileService>();
                services.AddSingleton<JobCandidateImportService>();
                services.AddSingleton<CandidateProfilesViewModel>();
                services.AddSingleton<JobsViewModel>();
                services.AddSingleton<ApplicationsViewModel>();
                services.AddSingleton<InsightsViewModel>();
            })
            .Build();
    }
}
