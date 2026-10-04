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
                    SequenceOrder = 0,

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
            0,
            breadcrumb.SequenceOrder);

        Assert.Equal(
            "http.request",
            breadcrumb.Category);

        Assert.Equal(
            "GET /api/users/{id}",
            breadcrumb.Action);

        Assert.Equal(
            "/api/users/{id}",
            breadcrumb.Target);

        Assert.Equal(
            "GET",
            breadcrumb.Data["method"]);

        Assert.Equal(
            "200",
            breadcrumb.Data["status_code"]);

        Assert.Equal(
            "25",
            breadcrumb.Data["duration_ms"]);
    }
}