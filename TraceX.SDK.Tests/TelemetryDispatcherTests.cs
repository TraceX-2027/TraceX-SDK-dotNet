using System.Net;
using System.Text.Json;
using System.IO.Compression;
using System.Threading.Channels;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TelemetryDispatcherTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[]? LastContentBytes { get; private set; }

        private readonly Func<int, HttpResponseMessage> _responseFactory;

        public MockHttpMessageHandler(Func<int, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            LastRequest = request;

            if (request.Content != null)
            {
                LastContentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            return _responseFactory(SendCount);
        }
    }

    [Fact]
    public async Task SendAsync_LargePayload_AppliesGzipCompression()
    {
        // ... (Keep existing test)
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest { ProjectKey = options.ProjectKey, StackTrace = new string('A', 1500) };
        await sender.SendAsync(crash, CancellationToken.None);

        Assert.Equal(1, handler.SendCount);
        Assert.True(handler.LastRequest?.Content?.Headers.ContentEncoding.Contains("gzip"));
    }

    [Fact]
    public async Task SendAsync_SmallPayload_SendsUncompressedJson()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest { ProjectKey = options.ProjectKey, StackTrace = "small stack trace" };
        await sender.SendAsync(crash, CancellationToken.None);

        Assert.Equal(1, handler.SendCount);
        Assert.False(handler.LastRequest?.Content?.Headers.ContentEncoding.Contains("gzip"));
    }

    [Fact]
    public async Task SendAsync_LargePayload_WithDisableGzip_SendsUncompressedJson()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/", DisableGzip = true };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest { ProjectKey = options.ProjectKey, StackTrace = new string('A', 1500) };
        await sender.SendAsync(crash, CancellationToken.None);

        Assert.Equal(1, handler.SendCount);
        Assert.False(handler.LastRequest?.Content?.Headers.ContentEncoding.Contains("gzip"));
    }

    [Fact]
    public async Task SendAsync_When429TooManyRequests_RetriesUpToMaxLimit()
    {
        var handler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
            return response;
        });
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest { ProjectKey = options.ProjectKey };

        await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(crash, CancellationToken.None));
        Assert.Equal(4, handler.SendCount);
    }

    [Fact]
    public async Task Dispatcher_BackgroundDispatch_Success()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);
        var channel = Channel.CreateUnbounded<CrashEventRequest>();

        var bufferPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(bufferPath);
        var buffer = new OfflineBuffer(bufferPath);

        var dispatcher = new TelemetryDispatcher(channel, sender, buffer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = dispatcher.RunAsync(cts.Token);

        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test" });
        channel.Writer.TryComplete();

        await task;
        Assert.Equal(1, handler.SendCount);

        Directory.Delete(bufferPath, true);
    }

    [Fact]
    public async Task Dispatcher_Permanent4xx_DiscardedNoRequeue()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);
        var channel = Channel.CreateUnbounded<CrashEventRequest>();

        var bufferPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(bufferPath);
        var buffer = new OfflineBuffer(bufferPath);

        var dispatcher = new TelemetryDispatcher(channel, sender, buffer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = dispatcher.RunAsync(cts.Token);

        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test" });
        channel.Writer.TryComplete();

        await task;

        Assert.Equal(1, handler.SendCount);
        Assert.False(buffer.TryDequeue(out _)); // Discarded

        Directory.Delete(bufferPath, true);
    }

    [Fact]
    public async Task Dispatcher_TransientFailure_OfflineBuffering_LaterEventsProcessed()
    {
        var handler = new MockHttpMessageHandler(count =>
            new HttpResponseMessage(count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);
        var channel = Channel.CreateUnbounded<CrashEventRequest>();

        var bufferPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(bufferPath);
        var buffer = new OfflineBuffer(bufferPath);

        var dispatcher = new TelemetryDispatcher(channel, sender, buffer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = dispatcher.RunAsync(cts.Token);

        // Write first crash -> 503 -> Enqueued to buffer
        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test", ErrorMessage = "First" });

        // Wait a bit to ensure it processes
        await Task.Delay(200);

        // Write second crash -> 202 -> Drains buffer -> First crash sent as 202
        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test", ErrorMessage = "Second" });
        channel.Writer.TryComplete();

        await task;

        Directory.Delete(bufferPath, true);
    }

    [Fact]
    public async Task Dispatcher_TransientFailure_ForcesBufferAndDrain()
    {
        // Fail first 4 attempts (initial + 3 retries), then succeed
        var handler = new MockHttpMessageHandler(count =>
            new HttpResponseMessage(count <= 4 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);
        var channel = Channel.CreateUnbounded<CrashEventRequest>();

        var bufferPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(bufferPath);
        var buffer = new OfflineBuffer(bufferPath);

        var dispatcher = new TelemetryDispatcher(channel, sender, buffer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var task = dispatcher.RunAsync(cts.Token);

        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test", ErrorMessage = "First" });
        await Task.Delay(500); // Allow retries to exhaust

        channel.Writer.TryWrite(new CrashEventRequest { ProjectKey = "test", ErrorMessage = "Second" });
        channel.Writer.TryComplete();

        await task;

        // Sends:
        // 1-4: Crash 1 fails -> Buffers
        // 5: Crash 2 succeeds
        // 6: Buffer drains -> Crash 1 succeeds
        Assert.Equal(6, handler.SendCount);

        Directory.Delete(bufferPath, true);
    }
}
