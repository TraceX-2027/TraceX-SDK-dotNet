using System.Reflection;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXClientBreadcrumbTests
{
    [Fact]
    public void CreateCrashEvent_IncludesBreadcrumbs()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        using var client =
            new TraceXClient(options);

        var breadcrumbs =
            new[]
            {
                new CrashBreadcrumb
                {
                    SequenceOrder =
                        1,

                    Timestamp =
                        DateTime.UtcNow,

                    Category =
                        "http.request",

                    Action =
                        "GET /api/users/{id}",

                    Target =
                        "/api/users/{id}",

                    Data =
                        new Dictionary<string, string>
                        {
                            ["method"] =
                                "GET",

                            ["route"] =
                                "/api/users/{id}",

                            ["status_code"] =
                                "200",

                            ["duration_ms"] =
                                "25"
                        }
                }
            };

        var exception =
            new InvalidOperationException(
                "breadcrumb integration test");

        var method =
            typeof(TraceXClient)
                .GetMethod(
                    "CreateCrashEvent",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        Assert.NotNull(method);

        var crash =
            Assert.IsType<CrashEventRequest>(
                method!.Invoke(
                    client,
                    new object[]
                    {
                        exception,
                        breadcrumbs
                    }));

        var breadcrumb =
            Assert.Single(
                crash.Breadcrumbs);

        Assert.Equal(
            1,
            breadcrumb.SequenceOrder);

        Assert.Equal(
            "network.http",
            breadcrumb.Category);

        Assert.Equal(
            "GET",
            breadcrumb.Action);

        Assert.Equal(
            "/api/users/{id}",
            breadcrumb.Target);

        Assert.Equal(
            "GET",
            breadcrumb.Data["method"]);

        Assert.Equal(
            "/api/users/{id}",
            breadcrumb.Data["route"]);

        Assert.Equal(
            "200",
            breadcrumb.Data["status_code"]);

        Assert.Equal(
            "25",
            breadcrumb.Data["duration_ms"]);
    }

    [Fact]
    public void AmbientBreadcrumbs_AreMappedToCrashBreadcrumbs()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        using var client =
            new TraceXClient(options);

        var buffer =
            new BreadcrumbBuffer();

        buffer.Add(
            new TraceXBreadcrumb
            {
                Timestamp =
                    DateTime.UtcNow,

                Type =
                    "http.request",

                Message =
                    "GET /api/orders",

                Data =
                    new Dictionary<string, string>
                    {
                        ["method"] =
                            "GET",

                        ["route"] =
                            "/api/orders",

                        ["status_code"] =
                            "500",

                        ["duration_ms"] =
                            "12"
                    }
            });

        TraceXBreadcrumbScope.Current =
            buffer;

        try
        {
            var ambient =
                TraceXBreadcrumbScope.Current!
                    .GetAll();

            var method =
                typeof(TraceXClient)
                    .GetMethod(
                        "CreateCrashEvent",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.NotNull(method);

            var crashBreadcrumbs =
                ambient
                    .Select(
                        (breadcrumb, index) =>
                            new CrashBreadcrumb
                            {
                                SequenceOrder =
                                    index + 1,

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

            var crash =
                Assert.IsType<CrashEventRequest>(
                    method!.Invoke(
                        client,
                        new object[]
                        {
                            new InvalidOperationException(
                                "ambient breadcrumb test"),
                            crashBreadcrumbs
                        }));

            var breadcrumb =
                Assert.Single(
                    crash.Breadcrumbs);

            Assert.Equal(
                1,
                breadcrumb.SequenceOrder);

            Assert.Equal(
                "network.http",
                breadcrumb.Category);

            Assert.Equal(
                "GET",
                breadcrumb.Action);

            Assert.Equal(
                "/api/orders",
                breadcrumb.Target);
        }
        finally
        {
            TraceXBreadcrumbScope.Current =
                null;
        }
    }
}