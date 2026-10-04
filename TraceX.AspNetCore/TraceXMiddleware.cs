using Microsoft.AspNetCore.Http;
using TraceX.Core;

namespace TraceX.AspNetCore;

public class TraceXMiddleware
{
    public const string ExceptionCapturedItemKey =
        "TraceX.ExceptionCaptured";

    private readonly RequestDelegate _next;
    private readonly TraceXClient _client;

    public TraceXMiddleware(
        RequestDelegate next,
        TraceXClient client)
    {
        _next = next;
        _client = client;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (!context.Items.ContainsKey(
                    ExceptionCapturedItemKey))
            {
                _client.CaptureException(
                    exception);

                context.Items[
                    ExceptionCapturedItemKey] = true;
            }

            throw;
        }
    }
}