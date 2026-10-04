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
            ProjectKey =
                "tracex_live_test123"
        };
    }

    private static CrashEventRequest CreateCrash(
        string errorMessage = "Test exception")
    {
        return new CrashEventRequest
        {
            OccurredAt =
                DateTime.UtcNow,

            Platform =
                "dotnet",

            Language =
                "csharp",

            ExceptionType =
                "System.Exception",

            ErrorMessage =
                errorMessage,

            StackTrace =
                "Test stack trace",

            Environment =
                new CrashEnvironment
                {
                    AppVersion =
                        "1.0.0",

                    DeviceModel =
                        "test-device",

                    RuntimeVersion =
                        ".NET 10",

                    OsName =
                        "Test OS",

                    OsVersion =
                        "1.0",

                    Hostname =
                        "test-host",

                    ProcessMemoryBytes =
                        12345,

                    EnvironmentName =
                        "Test"
                },

            Breadcrumbs =
                []
        };
    }

    [Fact]
    public async Task TraceXMiddleware_CapturesUnhandledException()
    {
        var options =
            CreateTestOptions();

        using var client =
            new TraceXClient(options);

        var expectedException =
            new InvalidOperationException(
                "Test exception");

        Exception? capturedException = null;

        client.ExceptionCaptured += exception =>
        {
            capturedException =
                exception;
        };

        var middleware =
            new TraceXMiddleware(
                _ => throw expectedException,
                client);

        var context =
            new DefaultHttpContext();

        await Assert.ThrowsAsync<
            InvalidOperationException>(
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

        Assert.Throws<InvalidOperationException>(
            () =>
                services.AddTraceX(
                    options =>
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
            CreateTempFilePath();

        try
        {
            var buffer =
                new OfflineBuffer(filePath);

            var crash =
                CreateCrash();

            buffer.Enqueue(
                crash);

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
            DeleteFileIfExists(
                filePath);
        }
    }

    [Fact]
    public void OfflineBuffer_LoadsPersistedCrashesOnStartup()
    {
        var filePath =
            CreateTempFilePath();

        try
        {
            var firstBuffer =
                new OfflineBuffer(
                    filePath);

            var crash =
                CreateCrash(
                    "Persisted crash");

            firstBuffer.Enqueue(
                crash);

            Assert.Equal(
                1,
                firstBuffer.Count);

            var secondBuffer =
                new OfflineBuffer(
                    filePath);

            Assert.Equal(
                1,
                secondBuffer.Count);

            var result =
                secondBuffer.TryDequeue(
                    out var restoredCrash);

            Assert.True(result);

            Assert.NotNull(
                restoredCrash);

            Assert.Equal(
                "Persisted crash",
                restoredCrash!.ErrorMessage);
        }
        finally
        {
            DeleteFileIfExists(
                filePath);
        }
    }

    [Fact]
    public async Task TelemetryDispatcher_SendsQueuedCrash()
    {
        var channel =
            Channel.CreateUnbounded<
                CrashEventRequest>();

        var filePath =
            CreateTempFilePath();

        try
        {
            var options =
                CreateTestOptions();

            var offlineBuffer =
                new OfflineBuffer(
                    filePath);

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
            DeleteFileIfExists(
                filePath);
        }
    }

    [Fact]
    public async Task TelemetryDispatcher_BuffersFailedCrash()
    {
        var channel =
            Channel.CreateUnbounded<
                CrashEventRequest>();

        var filePath =
            CreateTempFilePath();

        try
        {
            var options =
                CreateTestOptions();

            var offlineBuffer =
                new OfflineBuffer(
                    filePath);

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
            DeleteFileIfExists(
                filePath);
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

    [Fact]
    public async Task TelemetrySender_RetriesTransportFailures()
    {
        var options =
            CreateTestOptions();

        var handler =
            new SequenceHttpMessageHandler(
                new HttpRequestException(
                    "Temporary network failure."),

                new HttpRequestException(
                    "Temporary network failure."),

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

    [Fact]
    public async Task TelemetrySender_ThrowsOnUnauthorized()
    {
        var options =
            CreateTestOptions();

        var handler =
            new StubHttpMessageHandler(
                HttpStatusCode.Unauthorized);

        using var httpClient =
            new HttpClient(handler);

        var sender =
            new TelemetrySender(
                httpClient,
                options);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                sender.SendAsync(
                    CreateCrash(),
                    CancellationToken.None));

        Assert.Equal(
            1,
            handler.RequestCount);
    }

    [Fact]
    public async Task TelemetrySender_ThrowsOnUnexpectedClientError()
    {
        var options =
            CreateTestOptions();

        var handler =
            new StubHttpMessageHandler(
                HttpStatusCode.BadRequest);

        using var httpClient =
            new HttpClient(handler);

        var sender =
            new TelemetrySender(
                httpClient,
                options);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                sender.SendAsync(
                    CreateCrash(),
                    CancellationToken.None));

        Assert.Equal(
            1,
            handler.RequestCount);
    }

    [Fact]
    public async Task TelemetrySender_CompressesPayloadsLargerThanOneKilobyte()
    {
        var options =
            CreateTestOptions();

        var handler =
            new InspectingHttpMessageHandler(
                HttpStatusCode.OK);

        using var httpClient =
            new HttpClient(handler);

        var sender =
            new TelemetrySender(
                httpClient,
                options);

        var largeMessage =
            new string(
                'X',
                5000);

        await sender.SendAsync(
            CreateCrash(largeMessage),
            CancellationToken.None);

        Assert.NotNull(
            handler.Request);

        Assert.Equal(
            "gzip",
            handler.Request!
                .Content!
                .Headers
                .ContentEncoding
                .Single());

        Assert.Equal(
            "application/json",
            handler.Request
                .Content
                .Headers
                .ContentType!
                .MediaType);

        Assert.NotNull(
            handler.RequestBody);

        using var compressedStream =
            new MemoryStream(
                handler.RequestBody!);

        using var gzipStream =
            new GZipStream(
                compressedStream,
                CompressionMode.Decompress);

        using var reader =
            new StreamReader(
                gzipStream,
                Encoding.UTF8);

        var decompressedJson =
            await reader.ReadToEndAsync();

        Assert.Contains(
            largeMessage,
            decompressedJson);
    }

    [Fact]
    public void TraceXDataSanitizer_RedactsSensitiveValues()
    {
        var input =
            "Authorization: Bearer secret-token " +
            "Password=super-secret " +
            "api_key=my-api-key " +
            "token=another-secret";

        var sanitized =
            TraceXDataSanitizer.Sanitize(
                input);

        Assert.DoesNotContain(
            "secret-token",
            sanitized);

        Assert.DoesNotContain(
            "super-secret",
            sanitized);

        Assert.DoesNotContain(
            "my-api-key",
            sanitized);

        Assert.DoesNotContain(
            "another-secret",
            sanitized);

        Assert.Contains(
            "[REDACTED]",
            sanitized);
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"tracex-test-{Guid.NewGuid():N}.json");
    }

    private static void DeleteFileIfExists(
        string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode>
            _responses;

        public int RequestCount
        {
            get;
            private set;
        }

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

    private sealed class SequenceHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Queue<object>
            _responses;

        public int RequestCount
        {
            get;
            private set;
        }

        public SequenceHttpMessageHandler(
            params object[] responses)
        {
            _responses =
                new Queue<object>(
                    responses);
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestCount++;

            var response =
                _responses.Count > 0
                    ? _responses.Dequeue()
                    : HttpStatusCode.OK;

            if (response is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult(
                new HttpResponseMessage(
                    (HttpStatusCode)response));
        }
    }

    private sealed class InspectingHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public HttpRequestMessage? Request
        {
            get;
            private set;
        }

        public byte[]? RequestBody
        {
            get;
            private set;
        }

        public InspectingHttpMessageHandler(
            HttpStatusCode statusCode)
        {
            _statusCode =
                statusCode;
        }

        protected override async Task<
            HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Request =
                request;

            RequestBody =
                request.Content is null
                    ? null
                    : await request.Content.ReadAsByteArrayAsync(
                        cancellationToken);

            return new HttpResponseMessage(
                _statusCode);
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