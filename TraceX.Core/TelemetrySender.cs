using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace TraceX.Core;

public sealed class TelemetrySender
{
    private const int MaxRetries = 3;
    private const int CompressionThresholdBytes = 1024;

    private readonly HttpClient _httpClient;
    private readonly TraceXOptions _options;

    public TelemetrySender(
        HttpClient httpClient,
        TraceXOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task SendAsync(
        CrashEventRequest crash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(crash);

        var endpoint =
            $"{_options.Endpoint.TrimEnd('/')}/crashes";

        var json =
            JsonSerializer.Serialize(crash);

        var jsonBytes =
            Encoding.UTF8.GetBytes(json);

        for (var attempt = 0;
             attempt <= MaxRetries;
             attempt++)
        {
            try
            {
                using var request =
                    CreateRequest(
                        endpoint,
                        jsonBytes);

                using var response =
                    await _httpClient.SendAsync(
                        request,
                        cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if (response.StatusCode ==
                    HttpStatusCode.Unauthorized)
                {
                    throw new InvalidOperationException(
                        "TraceX API returned HTTP 401 Unauthorized. " +
                        "Telemetry dispatch has been stopped.");
                }

                if (response.StatusCode ==
                    HttpStatusCode.TooManyRequests)
                {
                    if (attempt == MaxRetries)
                    {
                        throw new HttpRequestException(
                            "TraceX API returned HTTP 429 after all retry attempts.",
                            null,
                            response.StatusCode);
                    }

                    await WaitForRetryAfterAsync(
                        response,
                        cancellationToken);

                    continue;
                }

                var statusCode =
                    (int)response.StatusCode;

                if (statusCode >= 500 &&
                    statusCode <= 599)
                {
                    if (attempt == MaxRetries)
                    {
                        throw new HttpRequestException(
                            $"TraceX API returned HTTP {statusCode} after all retry attempts.",
                            null,
                            response.StatusCode);
                    }

                    await WaitWithBackoffAsync(
                        attempt,
                        cancellationToken);

                    continue;
                }

                if (statusCode >= 400 &&
                    statusCode <= 499)
                {
                    throw new InvalidOperationException(
                        $"TraceX API returned unexpected HTTP {statusCode}. " +
                        "Telemetry dispatch has been stopped.");
                }

                throw new InvalidOperationException(
                    $"TraceX API returned unexpected HTTP status {statusCode}.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
                when (attempt < MaxRetries)
            {
                await WaitWithBackoffAsync(
                    attempt,
                    cancellationToken);
            }
            catch (TimeoutException)
                when (attempt < MaxRetries)
            {
                await WaitWithBackoffAsync(
                    attempt,
                    cancellationToken);
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested &&
                      attempt < MaxRetries)
            {
                await WaitWithBackoffAsync(
                    attempt,
                    cancellationToken);
            }
        }
    }

    private HttpRequestMessage CreateRequest(
        string endpoint,
        byte[] jsonBytes)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                endpoint);

        request.Headers.Add(
            "X-TraceX-Key",
            _options.ProjectKey);

        if (jsonBytes.Length >
            CompressionThresholdBytes)
        {
            request.Content =
                CreateGzipContent(
                    jsonBytes);
        }
        else
        {
            request.Content =
                new ByteArrayContent(
                    jsonBytes);

            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "application/json");
        }

        return request;
    }

    private static async Task WaitForRetryAfterAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var retryAfter =
            response.Headers.RetryAfter;

        if (retryAfter?.Delta is TimeSpan delta)
        {
            await Task.Delay(
                delta,
                cancellationToken);

            return;
        }

        if (retryAfter?.Date is DateTimeOffset date)
        {
            var delay =
                date - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(
                    delay,
                    cancellationToken);
            }

            return;
        }

        await Task.Delay(
            TimeSpan.FromMilliseconds(500),
            cancellationToken);
    }

    private static async Task WaitWithBackoffAsync(
        int attempt,
        CancellationToken cancellationToken)
    {
        var exponentialDelay =
            TimeSpan.FromMilliseconds(
                500 * Math.Pow(2, attempt));

        var jitter =
            TimeSpan.FromMilliseconds(
                Random.Shared.Next(
                    0,
                    250));

        await Task.Delay(
            exponentialDelay + jitter,
            cancellationToken);
    }

    private static HttpContent CreateGzipContent(
        byte[] data)
    {
        using var compressedStream =
            new MemoryStream();

        using (var gzipStream =
            new GZipStream(
                compressedStream,
                CompressionMode.Compress,
                leaveOpen: true))
        {
            gzipStream.Write(
                data,
                0,
                data.Length);
        }

        var compressedBytes =
            compressedStream.ToArray();

        var content =
            new ByteArrayContent(
                compressedBytes);

        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/json");

        content.Headers.ContentEncoding.Add(
            "gzip");

        return content;
    }
}