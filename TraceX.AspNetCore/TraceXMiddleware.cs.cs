using Microsoft.AspNetCore.Http;

namespace TraceX.AspNetCore;

public class TraceXMiddleware
{
    private readonly RequestDelegate _next;

    public TraceXMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}