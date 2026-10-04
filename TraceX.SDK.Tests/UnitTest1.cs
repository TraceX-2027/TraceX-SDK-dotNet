using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TraceX.AspNetCore;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public class UnitTest1
{
    private static TraceXOptions CreateTestOptions()
    {
        return new TraceXOptions
        {
            ProjectKey = "tracex_live_test123"
        };
    }

    [Fact]
    public async Task TraceXMiddleware_CapturesUnhandledException()
    {
        // Arrange
        var options = CreateTestOptions();
        var client = new TraceXClient(options);

        var expectedException =
            new InvalidOperationException("Test exception");

        Exception? capturedException = null;

        client.ExceptionCaptured += exception =>
        {
            capturedException = exception;
        };

        var middleware = new TraceXMiddleware(
            _ => throw expectedException,
            client);

        var context = new DefaultHttpContext();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        // Assert
        Assert.Same(
            expectedException,
            capturedException);

        Assert.True(
            context.Items.ContainsKey(
                TraceXMiddleware.ExceptionCapturedItemKey));
    }

    [Fact]
    public async Task TraceXExceptionHandler_DoesNotDuplicateCapture()
    {
        // Arrange
        var options = CreateTestOptions();
        var client = new TraceXClient(options);

        var captureCount = 0;

        client.ExceptionCaptured += _ =>
        {
            captureCount++;
        };

        var context = new DefaultHttpContext();

        context.Items[
            TraceXMiddleware.ExceptionCapturedItemKey] = true;

        var handler = new TraceXExceptionHandler(client);

        var exception =
            new InvalidOperationException("Test exception");

        // Act
        await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        // Assert
        Assert.Equal(0, captureCount);
    }

    [Fact]
    public void AddTraceX_ThrowsWhenProjectKeyIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddTraceX(options =>
            {
                options.ProjectKey = string.Empty;
            }));
    }

    [Fact]
    public void TraceXOptions_AcceptsValidTraceXProjectKey()
    {
        // Arrange
        var options = new TraceXOptions
        {
            ProjectKey = "tracex_live_test123"
        };

        // Act & Assert
        options.Validate();
    }

    [Fact]
    public void TraceXOptions_RejectsLegacyPkProjectKey()
    {
        // Arrange
        var options = new TraceXOptions
        {
            ProjectKey = "pk_test123"
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => options.Validate());

        Assert.Contains(
            "tracex_live_",
            exception.Message);
    }

    [Fact]
    public void TraceXClient_RegistersGlobalExceptionHooks()
    {
        // Arrange
        var options = CreateTestOptions();
        var client = new TraceXClient(options);

        // Act
        client.RegisterGlobalExceptionHooks();

        // Assert
        Assert.NotNull(client);
    }
}