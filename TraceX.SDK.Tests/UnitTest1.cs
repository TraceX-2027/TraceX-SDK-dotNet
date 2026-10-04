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
    [Fact]
    public async Task TraceXMiddleware_CapturesUnhandledException()
    {
        // Arrange
        var client = new TraceXClient(new TraceXOptions());

        var middleware = new TraceXMiddleware(
            _ => throw new InvalidOperationException("Test exception"),
            client);

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
        var client = new TraceXClient(new TraceXOptions());

        // Act
        client.RegisterGlobalExceptionHooks();

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task TelemetrySender_SendsCrashToApi()
    {
        // Arrange
        var handler = new TestHttpMessageHandler();

        using var httpClient = new HttpClient(handler);

        var options = CreateOptions();

        var sender = new TelemetrySender(
            httpClient,
            options);

        var crash = CreateCrash();

        // Act
        await sender.SendAsync(
            crash,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            HttpMethod.Post,
            handler.LastMethod);

        Assert.Equal(
            "https://example.test/api/v1/crashes",
            handler.LastUri);

        Assert.Equal(
            "pk_test",
            handler.LastApiKey);

        Assert.NotNull(
            handler.LastBody);

        Assert.Contains(
            "\"occurred_at\"",
            handler.LastBody);

        Assert.Contains(
            "\"platform\"",
            handler.LastBody);

        Assert.Contains(
            "\"exception_type\"",
            handler.LastBody);

        Assert.Contains(
            "\"error_message\"",
            handler.LastBody);

        Assert.Contains(
            "\"stack_trace\"",
            handler.LastBody);

        Assert.Null(
            handler.LastContentEncoding);
    }

    [Fact]
    public async Task TelemetrySender_CompressesPayloadLargerThanOneKilobyte()
    {
        // Arrange
        var handler = new TestHttpMessageHandler();

        using var httpClient = new HttpClient(handler);

        var sender = new TelemetrySender(
            httpClient,
            CreateOptions());

        var crash = CreateCrash();

        crash = new CrashEventRequest
        {
            OccurredAt = crash.OccurredAt,
            Platform = crash.Platform,
            Language = crash.Language,
            ExceptionType = crash.ExceptionType,
            ErrorMessage = crash.ErrorMessage,
            StackTrace = new string('A', 2000)
        };

        // Act
        await sender.SendAsync(
            crash,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            "gzip",
            handler.LastContentEncoding);

        Assert.NotNull(
            handler.LastBodyBytes);

        using var compressedStream = new MemoryStream(
            handler.LastBodyBytes);

        using var gzipStream = new GZipStream(
            compressedStream,
            CompressionMode.Decompress);

        using var reader = new StreamReader(
            gzipStream,
            Encoding.UTF8);

        var decompressedJson =
            await reader.ReadToEndAsync();

        Assert.Contains(
            "\"stack_trace\"",
            decompressedJson);

        Assert.Contains(
            new string('A', 2000),
            decompressedJson);
    }

    [Fact]
    public async Task TelemetrySender_RetriesOn503()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.Accepted);

        using var httpClient = new HttpClient(handler);

        var sender = new TelemetrySender(
            httpClient,
            CreateOptions());

        // Act
        await sender.SendAsync(
            CreateCrash(),
            CancellationToken.None);

        // Assert
        Assert.Equal(
            3,
            handler.RequestCount);
    }

    [Fact]
    public async Task TelemetrySender_StopsImmediatelyOn401()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Accepted);

        using var httpClient = new HttpClient(handler);

        var sender = new TelemetrySender(
            httpClient,
            CreateOptions());

        // Act
        await sender.SendAsync(
            CreateCrash(),
            CancellationToken.None);

        // Assert
        Assert.Equal(
            1,
            handler.RequestCount);
    }

    [Fact]
    public async Task TelemetrySender_RetriesOnNetworkFailure()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            new HttpRequestException("Network error"),
            HttpStatusCode.Accepted);

        using var httpClient = new HttpClient(handler);

        var sender = new TelemetrySender(
            httpClient,
            CreateOptions());

        // Act
        await sender.SendAsync(
            CreateCrash(),
            CancellationToken.None);

        // Assert
        Assert.Equal(
            2,
            handler.RequestCount);
    }

    [Fact]
    public async Task TelemetrySender_Respects429RetryAfter()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(
            new TestHttpResponse(
                HttpStatusCode.TooManyRequests,
                TimeSpan.FromMilliseconds(100)),
            HttpStatusCode.Accepted);

        using var httpClient = new HttpClient(handler);

        var sender = new TelemetrySender(
            httpClient,
            CreateOptions());

        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        // Act
        await sender.SendAsync(
            CreateCrash(),
            CancellationToken.None);

        stopwatch.Stop();

        // Assert
        Assert.Equal(
            2,
            handler.RequestCount);

        Assert.True(
            stopwatch.ElapsedMilliseconds >= 80,
            $"Expected Retry-After delay of at least 80ms, actual delay was {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public void OfflineBuffer_KeepsMaximum100CrashesUsingFifo()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        var filePath = Path.Combine(
            directory,
            "offline-buffer.json");

        try
        {
            var buffer = new OfflineBuffer(filePath);

            for (var i = 1; i <= 101; i++)
            {
                buffer.Enqueue(new CrashEventRequest
                {
                    OccurredAt = DateTime.UtcNow,
                    Platform = "dotnet",
                    Language = "csharp",
                    ExceptionType = $"Exception{i}",
                    ErrorMessage = $"Error{i}",
                    StackTrace = $"Stack{i}"
                });
            }

            // Assert
            Assert.Equal(
                100,
                buffer.Count);

            Assert.True(
                buffer.TryDequeue(out var first));

            Assert.NotNull(first);

            Assert.Equal(
                "Exception2",
                first!.ExceptionType);

            Assert.True(
                buffer.TryDequeue(out var second));

            Assert.NotNull(second);

            Assert.Equal(
                "Exception3",
                second!.ExceptionType);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public async Task TelemetryDispatcher_BuffersCrashAfterFailedDispatch()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        var filePath = Path.Combine(
            directory,
            "offline-buffer.json");

        try
        {
            var handler = new TestHttpMessageHandler(
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.ServiceUnavailable);

            using var httpClient = new HttpClient(handler);

            var sender = new TelemetrySender(
                httpClient,
                CreateOptions());

            var buffer = new OfflineBuffer(filePath);

            var channel =
                Channel.CreateUnbounded<CrashEventRequest>();

            var dispatcher = new TelemetryDispatcher(
                channel,
                sender,
                buffer);

            var crash = CreateCrash();

            // Act
            await channel.Writer.WriteAsync(
                crash);

            var dispatcherTask =
                dispatcher.RunAsync(
                    CancellationToken.None);

            while (buffer.Count == 0)
            {
                await Task.Delay(50);
            }

            // Assert
            Assert.Equal(
                1,
                buffer.Count);

            Assert.True(
                buffer.TryDequeue(
                    out var bufferedCrash));

            Assert.NotNull(
                bufferedCrash);

            Assert.Equal(
                crash.ExceptionType,
                bufferedCrash!.ExceptionType);

            Assert.Equal(
                4,
                handler.RequestCount);

            channel.Writer.TryComplete();

            await dispatcherTask;
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Fact]
    public void OfflineBuffer_PersistsCrashesToDisk()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        var filePath = Path.Combine(
            directory,
            "offline-buffer.json");

        try
        {
            var crash = CreateCrash();

            var firstBuffer =
                new OfflineBuffer(filePath);

            firstBuffer.Enqueue(crash);

            Assert.Equal(
                1,
                firstBuffer.Count);

            // Act
            var secondBuffer =
                new OfflineBuffer(filePath);

            // Assert
            Assert.Equal(
                1,
                secondBuffer.Count);

            Assert.True(
                secondBuffer.TryDequeue(
                    out var restoredCrash));

            Assert.NotNull(
                restoredCrash);

            Assert.Equal(
                crash.ExceptionType,
                restoredCrash!.ExceptionType);

            Assert.Equal(
                crash.ErrorMessage,
                restoredCrash.ErrorMessage);

            Assert.Equal(
                crash.StackTrace,
                restoredCrash.StackTrace);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static TraceXOptions CreateOptions()
    {
        return new TraceXOptions
        {
            ProjectKey = "pk_test",
            Endpoint = "https://example.test/api/v1/"
        };
    }

    private static CrashEventRequest CreateCrash()
    {
        return new CrashEventRequest
        {
            OccurredAt = DateTime.UtcNow,
            Platform = "dotnet",
            Language = "csharp",
            ExceptionType =
                "System.InvalidOperationException",
            ErrorMessage = "Test exception",
            StackTrace = "Test stack trace"
        };
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "TraceX.Tests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    private sealed class TestHttpResponse
    {
        public TestHttpResponse(
            HttpStatusCode statusCode,
            TimeSpan retryAfter)
        {
            StatusCode = statusCode;
            RetryAfter = retryAfter;
        }

        public HttpStatusCode StatusCode { get; }

        public TimeSpan RetryAfter { get; }
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> _responses;

        public TestHttpMessageHandler(
            params object[] responses)
        {
            _responses = new Queue<object>(
                responses);
        }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastUri { get; private set; }

        public string? LastApiKey { get; private set; }

        public string? LastBody { get; private set; }

        public byte[]? LastBodyBytes { get; private set; }

        public string? LastContentEncoding { get; private set; }

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            LastMethod = request.Method;

            LastUri =
                request.RequestUri?.ToString();

            if (request.Headers.TryGetValues(
                "X-TraceX-Key",
                out var values))
            {
                LastApiKey = values.Single();
            }

            if (request.Content is not null)
            {
                LastBodyBytes =
                    await request.Content.ReadAsByteArrayAsync(
                        cancellationToken);

                LastBody =
                    Encoding.UTF8.GetString(
                        LastBodyBytes);

                LastContentEncoding =
                    request.Content.Headers
                        .ContentEncoding
                        .FirstOrDefault();
            }

            var response =
                _responses.Count > 0
                    ? _responses.Dequeue()
                    : HttpStatusCode.Accepted;

            if (response is Exception exception)
            {
                throw exception;
            }

            if (response is TestHttpResponse testResponse)
            {
                var httpResponse =
                    new HttpResponseMessage(
                        testResponse.StatusCode);

                httpResponse.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(
                        testResponse.RetryAfter);

                return httpResponse;
            }

            return new HttpResponseMessage(
                (HttpStatusCode)response);
        }
    }
}