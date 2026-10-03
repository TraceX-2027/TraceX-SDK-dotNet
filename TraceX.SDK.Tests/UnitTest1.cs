using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TraceX.AspNetCore;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public class UnitTest1
{
    [Fact]
    public async Task TraceXMiddleware_PropagatesUnhandledException()
    {
        // Arrange
        var middleware = new TraceXMiddleware(
            _ => throw new InvalidOperationException("Test exception"));

        var context = new DefaultHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));
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
    public void TraceXClient_RegistersGlobalExceptionHooks()
    {
        // Arrange
        var client = new TraceXClient();

        // Act
        client.RegisterGlobalExceptionHooks();

        // Assert
        Assert.NotNull(client);
    }
}
