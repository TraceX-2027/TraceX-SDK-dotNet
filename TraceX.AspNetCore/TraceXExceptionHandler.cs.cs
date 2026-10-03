using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using TraceX.Core;

namespace TraceX.AspNetCore;

public sealed class TraceXExceptionHandler : IExceptionHandler
{
    private readonly TraceXClient _client;

    public TraceXExceptionHandler(TraceXClient client)
    {
        _client = client;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _client.CaptureException(exception);

        await Task.CompletedTask;

        return false;
    }
}