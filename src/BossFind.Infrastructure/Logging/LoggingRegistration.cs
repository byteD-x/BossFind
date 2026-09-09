using Microsoft.Extensions.Hosting;
using Serilog;

namespace BossFind.Infrastructure.Logging;

public static class LoggingRegistration
{
    public static IHostBuilder UseBossFindLogging(this IHostBuilder hostBuilder, string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        return hostBuilder.UseSerilog((_, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "bossfind-.log"),
                rollingInterval: RollingInterval.Day,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                retainedFileCountLimit: 14,
                shared: true));
    }
}
