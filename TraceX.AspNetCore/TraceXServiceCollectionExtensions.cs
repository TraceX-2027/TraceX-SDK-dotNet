using Microsoft.Extensions.DependencyInjection;
using TraceX.Core;

namespace TraceX.AspNetCore;

public static class TraceXServiceCollectionExtensions
{
    public static IServiceCollection AddTraceX(
        this IServiceCollection services,
        Action<TraceXOptions> configure)
    {
        var options = new TraceXOptions();

        configure(options);
        options.Validate();

        services.AddSingleton(options);

        services.AddSingleton(sp =>
        {
            var configuredOptions =
                sp.GetRequiredService<TraceXOptions>();

            var client =
                new TraceXClient(configuredOptions);

            client.RegisterGlobalExceptionHooks();

            return client;
        });

        services.AddExceptionHandler<TraceXExceptionHandler>();

        return services;
    }
}