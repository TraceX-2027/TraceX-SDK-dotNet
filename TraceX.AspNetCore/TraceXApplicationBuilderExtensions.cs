using Microsoft.AspNetCore.Builder;

namespace TraceX.AspNetCore;

public static class TraceXApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTraceX(
        this IApplicationBuilder app)
    {
        app.UseExceptionHandler();

        return app.UseMiddleware<TraceXMiddleware>();
    }
}