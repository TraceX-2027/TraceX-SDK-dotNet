using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TraceX.AspNetCore;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXMiddlewareExceptionTests
{


    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_CapturesAndReThrows()
    {
        // Arrange
        var options = new TraceXOptions { ProjectKey = "pk_test", Endpoint = "https://example.com/" };
        await using var client = new TraceXClient(options);
        
        Exception? capturedException = null;
        client.ExceptionCaptured += ex => capturedException = ex;

        var expectedException = new InvalidOperationException("Test exception");
        var middleware = new TraceXMiddleware(
            context => throw expectedException,
            client);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/crash";

        // Act
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        // Assert
        Assert.Same(expectedException, thrownException);
        Assert.Same(expectedException, capturedException);
        
        var buffer = context.Items[TraceXMiddleware.BreadcrumbBufferItemKey] as BreadcrumbBuffer;
        Assert.NotNull(buffer);
        
        var breadcrumbs = buffer.GetAll();
        Assert.Single(breadcrumbs);
        Assert.Equal("500", breadcrumbs[0].Data["status_code"]?.ToString());
        
        Assert.True(context.Items.ContainsKey(TraceXMiddleware.ExceptionCapturedItemKey));
        Assert.True((bool)context.Items[TraceXMiddleware.ExceptionCapturedItemKey]!);
    }
}
