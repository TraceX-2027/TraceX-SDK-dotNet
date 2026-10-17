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

            try
            {
                AddRequestBreadcrumb(
                    context,
                    breadcrumbBuffer,
                    stopwatch.ElapsedMilliseconds,
                    context.Response.StatusCode);
            }
            catch
            {
                // Ignore
            }
        }
        catch (Exception exception)
        {
            try
            {
                AddRequestBreadcrumb(
                    context,
                    breadcrumbBuffer,
                    stopwatch.ElapsedMilliseconds,
                    StatusCodes.Status500InternalServerError);
            }
            catch
            {
                // Ignore
            }

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
            TraceXBreadcrumbScope.Current =
                null;
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
            .Select((breadcrumb, index) =>
            {
                var category =
                    breadcrumb.Type.StartsWith(
                        "http.",
                        StringComparison.OrdinalIgnoreCase)
                        ? "network.http"
                        : Clamp(
                            breadcrumb.Type,
                            50);

                var action =
                    breadcrumb.Data.TryGetValue(
                            "method",
                            out var method) &&
                        !string.IsNullOrEmpty(method)
                        ? method
                        : breadcrumb.Message;

                var target =
                    breadcrumb.Data.TryGetValue(
                            "route",
                            out var route) &&
                        !string.IsNullOrEmpty(route)
                        ? route
                        : breadcrumb.Data.TryGetValue(
                                "url",
                                out var url) &&
                            !string.IsNullOrEmpty(url)
                            ? url
                            : string.Empty;

                return new CrashBreadcrumb
                {
                    SequenceOrder =
                        index + 1,

                    Timestamp =
                        breadcrumb.Timestamp,

                    Category =
                        category,

                    Action =
                        Clamp(
                            action,
                            100),

                    Target =
                        Clamp(
                            target,
                            255),

                    Data =
                        new Dictionary<string, string>(
                            breadcrumb.Data)
                };
            })
            .ToList();
    }

    private static string GetRouteTemplate(
        HttpContext context)
    {
        var routeEndpoint =
            context.GetEndpoint()
                as RouteEndpoint;

        return routeEndpoint?.RoutePattern.RawText
            ?? context.Request.Path.Value
            ?? "/";
    }

    private static string Clamp(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}