using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BossFind.Infrastructure.Persistence;

public static class DbContextRegistration
{
    public static IServiceCollection AddBossFindPersistence(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        return services;
    }
}
