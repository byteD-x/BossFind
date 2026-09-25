using BossFind.Application.Common;
using BossFind.Application.Profiles;
using BossFind.Application.Insights;
using BossFind.Application.Jobs;
using BossFind.Application.Matching;
using BossFind.Infrastructure.Export;
using BossFind.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BossFind.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBossFindInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<CandidateProfileRepository>();
        services.AddSingleton<ICandidateProfileRepository>(services => services.GetRequiredService<CandidateProfileRepository>());
        services.AddSingleton<IJobRepository>(services => services.GetRequiredService<CandidateProfileRepository>());
        services.AddSingleton<ICandidateProfileExportService, JsonCandidateProfileExportService>();
        services.AddSingleton<ILocalDataArchiveService>(services => new LocalDataArchiveService(
            databasePath,
            services.GetRequiredService<IJobRepository>()));
        services.AddSingleton<JobPostingService>();
        services.AddSingleton<IJobInsightService, LocalJobInsightService>();
        services.AddSingleton<IResumeJobMatchService, HybridResumeJobMatchService>();
        services.AddBossFindPersistence(databasePath);
        return services;
    }
}
