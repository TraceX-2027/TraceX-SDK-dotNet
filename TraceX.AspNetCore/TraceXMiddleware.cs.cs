using Microsoft.AspNetCore.Http;
using TraceX.Core;

namespace TraceX.AspNetCore;

public class TraceXMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TraceXClient _client;

    public TraceXMiddleware(
        RequestDelegate next,
        TraceXClient client)
    {
        _next = next;
        _client = client;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _client.CaptureException(exception);
            throw;
        }
    }
}