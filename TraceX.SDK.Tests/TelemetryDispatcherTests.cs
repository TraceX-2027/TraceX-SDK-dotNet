using System.Net;
using System.Text.Json;
using System.IO.Compression;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TelemetryDispatcherTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public byte[]? LastContentBytes { get; private set; }

        private readonly Func<HttpResponseMessage> _responseFactory;

        public MockHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
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

            return _responseFactory();
        }
    }

    [Fact]
    public async Task SendAsync_LargePayload_AppliesGzipCompression()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        // Create a large payload > 1KB
        var crash = new CrashEventRequest
        {
            ProjectKey = options.ProjectKey,
            StackTrace = new string('A', 1500)
        };

        // Act
        await sender.SendAsync(crash, CancellationToken.None);

        // Assert
        Assert.Equal(1, handler.SendCount);
        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Content?.Headers.ContentEncoding.Contains("gzip"));
        Assert.Equal("application/json", handler.LastRequest.Content?.Headers.ContentType?.MediaType);
        
        Assert.NotNull(handler.LastContentBytes);
        Assert.True(handler.LastContentBytes.Length > 0);
        Assert.True(handler.LastContentBytes.Length < 1500); // Should be compressed
    }
    
    [Fact]
    public async Task SendAsync_SmallPayload_SendsUncompressedJson()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest
        {
            ProjectKey = options.ProjectKey,
            StackTrace = "small stack trace"
        };

        // Act
        await sender.SendAsync(crash, CancellationToken.None);

        // Assert
        Assert.Equal(1, handler.SendCount);
        Assert.NotNull(handler.LastRequest);
        Assert.False(handler.LastRequest.Content?.Headers.ContentEncoding.Contains("gzip"));
        Assert.Equal("application/json", handler.LastRequest.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SendAsync_When429TooManyRequests_RetriesUpToMaxLimit()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(() => 
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(50));
            return response;
        });
        var client = new HttpClient(handler);
        var options = new TraceXOptions { ProjectKey = "tracex_live_test", Endpoint = "https://test/" };
        var sender = new TelemetrySender(client, options);

        var crash = new CrashEventRequest { ProjectKey = options.ProjectKey };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(crash, CancellationToken.None));
        
        // MaxRetries is 3 in TelemetrySender, so it sends initially + 3 retries = 4 sends
        Assert.Equal(4, handler.SendCount);
    }
}
