using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TraceX.Core;

namespace TraceX.AspNetCore;

public class TraceXMiddleware
{
    public const string BreadcrumbBufferItemKey =
        "TraceX.BreadcrumbBuffer";

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
        var breadcrumbBuffer =
            new BreadcrumbBuffer();

        context.Items[
            BreadcrumbBufferItemKey] =
            breadcrumbBuffer;

        TraceXBreadcrumbScope.Current =
            breadcrumbBuffer;

        var stopwatch =
            Stopwatch.StartNew();

        try
        {
            await _next(context);

            AddRequestBreadcrumb(
                context,
                breadcrumbBuffer,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
        catch (Exception exception)
        {
            AddRequestBreadcrumb(
                context,
                breadcrumbBuffer,
                stopwatch.ElapsedMilliseconds,
                StatusCodes.Status500InternalServerError);

            if (!context.Items.ContainsKey(
                    ExceptionCapturedItemKey))
            {
                _client.CaptureException(
                    exception,
                    ToCrashBreadcrumbs(
                        breadcrumbBuffer.GetAll()));

                context.Items[
                    ExceptionCapturedItemKey] =
                    true;
            }

            throw;
        }
        finally
        {
            TraceXBreadcrumbScope.Current = null;
        }
    }

    private static void AddRequestBreadcrumb(
        HttpContext context,
        BreadcrumbBuffer breadcrumbBuffer,
        long durationMilliseconds,
        int statusCode)
    {
        var routeTemplate =
            GetRouteTemplate(context);

        var queryString =
            context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value!
                : string.Empty;

        var sanitizedQuery =
            TraceXDataSanitizer.Sanitize(
                queryString);

        breadcrumbBuffer.Add(
            new TraceXBreadcrumb
            {
                Timestamp =
                    DateTime.UtcNow,

                Type =
                    "http.request",

                Message =
                    $"{context.Request.Method} {routeTemplate}",

                Data =
                    new Dictionary<string, string>
                    {
                        ["method"] =
                            context.Request.Method,

                        ["route"] =
                            TraceXDataSanitizer.Sanitize(
                                routeTemplate),

                        ["query"] =
                            sanitizedQuery,

                        ["status_code"] =
                            statusCode.ToString(),

                        ["duration_ms"] =
                            durationMilliseconds.ToString()
                    }
            });
    }

    private static IReadOnlyList<CrashBreadcrumb>
        ToCrashBreadcrumbs(
            IReadOnlyList<TraceXBreadcrumb> breadcrumbs)
    {
        return breadcrumbs
            .Select(
                (breadcrumb, index) =>
                    new CrashBreadcrumb
                    {
                        SequenceOrder =
                            index,

                        Timestamp =
                            breadcrumb.Timestamp,

                        Category =
                            breadcrumb.Type,

                        Action =
                            breadcrumb.Message,

                        Target =
                            breadcrumb.Data.TryGetValue(
                                "route",
                                out var route)
                                ? route
                                : string.Empty,

                        Data =
                            new Dictionary<string, string>(
                                breadcrumb.Data)
                    })
            .ToList();
    }

    private static string GetRouteTemplate(
        HttpContext context)
    {
        var routeEndpoint =
            context.GetEndpoint() as RouteEndpoint;

        return routeEndpoint?.RoutePattern.RawText
            ?? context.Request.Path.Value
            ?? "/";
    }
}