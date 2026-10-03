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

        var client = new TraceXClient();
        client.RegisterGlobalExceptionHooks();

        services.AddSingleton(client);

        services.AddExceptionHandler<TraceXExceptionHandler>();

        return services;
    }
}