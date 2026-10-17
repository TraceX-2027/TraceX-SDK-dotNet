using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using TraceX.AspNetCore;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXMiddlewareBreadcrumbTests
{
    [Fact]
    public async Task InvokeAsync_CapturesRequestBreadcrumb()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        var client =
            new TraceXClient(options);

        var middleware =
            new TraceXMiddleware(
                context =>
                {
                    context.Response.StatusCode =
                        StatusCodes.Status201Created;

                    return Task.CompletedTask;
                },
                client);

        var context =
            new DefaultHttpContext();

        context.Request.Method =
            "GET";

        context.Request.Path =
            "/api/users";

        context.Request.QueryString =
            new QueryString(
                "?page=2&sort=name");

        await middleware.InvokeAsync(
            context);

        var buffer =
            Assert.IsType<BreadcrumbBuffer>(
                context.Items[
                    TraceXMiddleware.BreadcrumbBufferItemKey]);

        var breadcrumbs =
            buffer.GetAll();

        var breadcrumb =
            Assert.Single(breadcrumbs);

        Assert.Equal(
            "http.request",
            breadcrumb.Type);

        Assert.Equal(
            "GET /api/users",
            breadcrumb.Message);

        Assert.Equal(
            "GET",
            breadcrumb.Data["method"]);

        Assert.Equal(
            "/api/users",
            breadcrumb.Data["route"]);

        Assert.Equal(
            "?page=2&sort=name",
            breadcrumb.Data["query"]);

        Assert.Equal(
            "201",
            breadcrumb.Data["status_code"]);

        Assert.True(
            long.TryParse(
                breadcrumb.Data["duration_ms"],
                out var duration));

        Assert.True(
            duration >= 0);

        Assert.Null(
            TraceXBreadcrumbScope.Current);
    }

    [Fact]
    public async Task InvokeAsync_SanitizesSensitiveQueryParameters()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        var client =
            new TraceXClient(options);

        var middleware =
            new TraceXMiddleware(
                context =>
                    Task.CompletedTask,
                client);

        var context =
            new DefaultHttpContext();

        context.Request.Method =
            "POST";

        context.Request.Path =
            "/login";

        context.Request.QueryString =
            new QueryString(
                "?username=youssef&password=secret123");

        await middleware.InvokeAsync(
            context);

        var buffer =
            Assert.IsType<BreadcrumbBuffer>(
                context.Items[
                    TraceXMiddleware.BreadcrumbBufferItemKey]);

        var breadcrumb =
            Assert.Single(
                buffer.GetAll());

        Assert.Equal(
            "?username=youssef&password=[REDACTED]",
            breadcrumb.Data["query"]);
    }

    [Fact]
    public async Task InvokeAsync_UsesRouteTemplateWhenAvailable()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        var client =
            new TraceXClient(options);

        var middleware =
            new TraceXMiddleware(
                context =>
                {
                    context.Response.StatusCode =
                        StatusCodes.Status200OK;

                    return Task.CompletedTask;
                },
                client);

        var context =
            new DefaultHttpContext();

        context.Request.Method =
            "GET";

        context.Request.Path =
            "/api/users/42";

        var routePattern =
            RoutePatternFactory.Parse(
                "/api/users/{id}");

        var routeEndpoint =
            new RouteEndpointBuilder(
                _ => Task.CompletedTask,
                routePattern,
                order: 0)
            {
                DisplayName =
                    "GET /api/users/{id}"
            }
            .Build();

        context.SetEndpoint(
            routeEndpoint);

        await middleware.InvokeAsync(
            context);

        var buffer =
            Assert.IsType<BreadcrumbBuffer>(
                context.Items[
                    TraceXMiddleware.BreadcrumbBufferItemKey]);

        var breadcrumb =
            Assert.Single(
                buffer.GetAll());

        Assert.Equal(
            "GET /api/users/{id}",
            breadcrumb.Message);

        Assert.Equal(
            "/api/users/{id}",
            breadcrumb.Data["route"]);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotBleedBreadcrumbsAcrossRequests()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test",

                Endpoint =
                    "https://example.com/"
            };

        var client =
            new TraceXClient(options);

        var middleware =
            new TraceXMiddleware(
                context =>
                    Task.CompletedTask,
                client);

        var firstContext =
            new DefaultHttpContext();

        firstContext.Request.Method =
            "GET";

        firstContext.Request.Path =
            "/first";

        await middleware.InvokeAsync(
            firstContext);

        var firstBuffer =
            Assert.IsType<BreadcrumbBuffer>(
                firstContext.Items[
                    TraceXMiddleware.BreadcrumbBufferItemKey]);

        var secondContext =
            new DefaultHttpContext();

        secondContext.Request.Method =
            "POST";

        secondContext.Request.Path =
            "/second";

        await middleware.InvokeAsync(
            secondContext);

        var secondBuffer =
            Assert.IsType<BreadcrumbBuffer>(
                secondContext.Items[
                    TraceXMiddleware.BreadcrumbBufferItemKey]);

        Assert.NotSame(
            firstBuffer,
            secondBuffer);

        Assert.Single(
            firstBuffer.GetAll());

        Assert.Single(
            secondBuffer.GetAll());

        Assert.Equal(
            "GET /first",
            firstBuffer.GetAll()[0].Message);

        Assert.Equal(
            "POST /second",
            secondBuffer.GetAll()[0].Message);

        Assert.Null(
            TraceXBreadcrumbScope.Current);
    }
}