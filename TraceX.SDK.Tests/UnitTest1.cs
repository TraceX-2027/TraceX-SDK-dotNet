using System.Net;
using System.Net.Http;
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

    private static CrashEventRequest CreateCrash()
    {
        return new CrashEventRequest
        {
            OccurredAt = DateTime.UtcNow,
            Platform = "dotnet",
            Language = "csharp",
            ExceptionType = "System.Exception",
            ErrorMessage = "Test exception",
            StackTrace = "Test stack trace",
            Environment = new CrashEnvironment
            {
                RuntimeVersion = ".NET 9",
                OsName = "Test OS",
                OsVersion = "1.0",
                Hostname = "test-host",
                ProcessMemoryBytes = 12345,
                EnvironmentName = "Test"
            },
            Breadcrumbs = []
        };
    }

    [Fact]
    public async Task TraceXMiddleware_CapturesUnhandledException()
    {
        var options = CreateTestOptions();

        using var client =
            new TraceXClient(options);

        var expectedException =
            new InvalidOperationException(
                "Test exception");

        Exception? capturedException = null;

        client.ExceptionCaptured += exception =>
        {
            capturedException = exception;
        };

        var middleware =
            new TraceXMiddleware(
                _ => throw expectedException,
                client);

        var context =
            new DefaultHttpContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

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
        var options =
            CreateTestOptions();

        using var client =
            new TraceXClient(options);

        var captureCount = 0;

        client.ExceptionCaptured += _ =>
        {
            captureCount++;
        };

        var context =
            new DefaultHttpContext();

        context.Items[
            TraceXMiddleware.ExceptionCapturedItemKey] =
            true;

        var handler =
            new TraceXExceptionHandler(client);

        var exception =
            new InvalidOperationException(
                "Test exception");

        await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.Equal(
            0,
            captureCount);
    }

    [Fact]
    public void AddTraceX_ThrowsWhenProjectKeyIsMissing()
    {
        var services =
            new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddTraceX(options =>
            {
                options.ProjectKey =
                    string.Empty;
            }));
    }

    [Fact]
    public void TraceXOptions_AcceptsValidTraceXProjectKey()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "tracex_live_test123"
            };

        options.Validate();
    }

    [Fact]
    public void TraceXOptions_RejectsLegacyPkProjectKey()
    {
        var options =
            new TraceXOptions
            {
                ProjectKey =
                    "pk_test123"
            };

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => options.Validate());

        Assert.Contains(
            "tracex_live_",
            exception.Message);
    }

    [Fact]
    public void TraceXClient_RegistersGlobalExceptionHooks()
    {
        var options =
            CreateTestOptions();

        using var client =
            new TraceXClient(options);

        client.RegisterGlobalExceptionHooks();

        Assert.NotNull(client);
    }

    [Fact]
    public void OfflineBuffer_EnqueueAndDequeue_Works()
    {
        var filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"tracex-test-{Guid.NewGuid():N}.json");

        try
        {
            var buffer =
                new OfflineBuffer(filePath);

            var crash =
                CreateCrash();

            buffer.Enqueue(crash);

            Assert.Equal(
                1,
                buffer.Count);

            var result =
                buffer.TryDequeue(
                    out var cachedCrash);

            Assert.True(result);

            Assert.NotNull(cachedCrash);

            Assert.Equal(
                crash.ErrorMessage,
                cachedCrash!.ErrorMessage);

            Assert.Equal(
                0,
                buffer.Count);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task TelemetryDispatcher_SendsQueuedCrash()
    {
        var channel =
            Channel.CreateUnbounded<CrashEventRequest>();

        var filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"tracex-test-{Guid.NewGuid():N}.json");

        try
        {
            var options =
                CreateTestOptions();

            var offlineBuffer =
                new OfflineBuffer(filePath);

            using var httpClient =
                new HttpClient(
                    new StubHttpMessageHandler(
                        HttpStatusCode.OK));

            var sender =
                new TelemetrySender(
                    httpClient,
                    options);

            var dispatcher =
                new TelemetryDispatcher(
                    channel,
                    sender,
                    offlineBuffer);

            var crash =
                CreateCrash();

            await channel.Writer.WriteAsync(
                crash);

            channel.Writer.TryComplete();

            await dispatcher.RunAsync(
                CancellationToken.None);

            Assert.Equal(
                0,
                offlineBuffer.Count);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task TelemetryDispatcher_BuffersFailedCrash()
    {
        var channel =
            Channel.CreateUnbounded<CrashEventRequest>();

        var filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"tracex-test-{Guid.NewGuid():N}.json");

        try
        {
            var options =
                CreateTestOptions();

            var offlineBuffer =
                new OfflineBuffer(filePath);

            using var httpClient =
                new HttpClient(
                    new ThrowingHttpMessageHandler());

            var sender =
                new TelemetrySender(
                    httpClient,
                    options);

            var dispatcher =
                new TelemetryDispatcher(
                    channel,
                    sender,
                    offlineBuffer);

            await channel.Writer.WriteAsync(
                CreateCrash());

            channel.Writer.TryComplete();

            await dispatcher.RunAsync(
                CancellationToken.None);

            Assert.Equal(
                1,
                offlineBuffer.Count);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task TelemetrySender_RetriesServerErrors()
    {
        var options =
            CreateTestOptions();

        var handler =
            new StubHttpMessageHandler(
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.OK);

        using var httpClient =
            new HttpClient(handler);

        var sender =
            new TelemetrySender(
                httpClient,
                options);

        await sender.SendAsync(
            CreateCrash(),
            CancellationToken.None);

        Assert.Equal(
            3,
            handler.RequestCount);
    }

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses;

        public int RequestCount { get; private set; }

        public StubHttpMessageHandler(
            params HttpStatusCode[] responses)
        {
            _responses =
                new Queue<HttpStatusCode>(
                    responses);
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestCount++;

            var statusCode =
                _responses.Count > 0
                    ? _responses.Dequeue()
                    : HttpStatusCode.OK;

            return Task.FromResult(
                new HttpResponseMessage(
                    statusCode));
        }
    }

    private sealed class ThrowingHttpMessageHandler
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            throw new HttpRequestException(
                "Simulated network failure.");
        }
    }
}