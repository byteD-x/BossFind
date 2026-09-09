using BossFind.Application.Common;
using BossFind.Application.Profiles;
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
        services.AddSingleton<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddSingleton<ICandidateProfileExportService, JsonCandidateProfileExportService>();
        services.AddBossFindPersistence(databasePath);
        return services;
    }
}
