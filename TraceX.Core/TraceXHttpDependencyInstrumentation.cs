using System.Diagnostics;
using System.Net.Http;

namespace TraceX.Core;

public static class TraceXHttpDependencyInstrumentation
{
    private const string DiagnosticListenerName =
        "HttpHandlerDiagnosticListener";

    private const string RequestActivityName =
        "System.Net.Http.HttpRequestOut";

    private const string RequestStartEvent =
        "System.Net.Http.HttpRequestOut.Start";

    private const string RequestStopEvent =
        "System.Net.Http.HttpRequestOut.Stop";

    internal static readonly HttpRequestOptionsKey<bool>
        InternalRequestKey =
            new("TraceX.InternalRequest");

    private static readonly HttpRequestOptionsKey<long>
        StartTimestampKey =
            new("TraceX.StartTimestamp");

    private static readonly object Sync =
        new();

    private static readonly List<IDisposable>
        ListenerSubscriptions =
            new();

    private static IDisposable? _allListenersSubscription;
    private static int _referenceCount;

    public static IDisposable EnsureStarted()
    {
        lock (Sync)
        {
            _referenceCount++;

            if (_referenceCount == 1)
            {
                _allListenersSubscription =
                    DiagnosticListener.AllListeners.Subscribe(
                        new ListenerObserver());
            }
        }

        return new Registration();
    }

    private static void Release()
    {
        List<IDisposable> subscriptions;

        lock (Sync)
        {
            if (_referenceCount == 0)
            {
                return;
            }

            _referenceCount--;

            if (_referenceCount != 0)
            {
                return;
            }

            _allListenersSubscription?.Dispose();

            _allListenersSubscription =
                null;

            subscriptions =
                ListenerSubscriptions.ToList();

            ListenerSubscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    private sealed class Registration
        : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(
                    ref _disposed,
                    1) != 0)
            {
                return;
            }

            Release();
        }
    }

    private sealed class ListenerObserver
        : IObserver<DiagnosticListener>
    {
        public void OnNext(
            DiagnosticListener listener)
        {
            if (!string.Equals(
                    listener.Name,
                    DiagnosticListenerName,
                    StringComparison.Ordinal))
            {
                return;
            }

            var subscription =
                listener.Subscribe(
                    new HttpEventObserver(),
                    static eventName =>
                        eventName ==
                            RequestActivityName ||
                        eventName ==
                            RequestStartEvent ||
                        eventName ==
                            RequestStopEvent);

            lock (Sync)
            {
                if (_referenceCount == 0)
                {
                    subscription.Dispose();
                }
                else
                {
                    ListenerSubscriptions.Add(
                        subscription);
                }
            }
        }

        public void OnError(
            Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private sealed class HttpEventObserver
        : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(
            KeyValuePair<string, object?> value)
        {
            try
            {
                if (value.Value is null)
                {
                    return;
                }

                var request =
                    GetProperty<HttpRequestMessage>(
                        value.Value,
                        "Request");

                if (request is null)
                {
                    return;
                }

                if (request.Options.TryGetValue(
                        InternalRequestKey,
                        out bool isInternal) &&
                    isInternal)
                {
                    return;
                }

                if (value.Key ==
                    RequestStartEvent)
                {
                    HandleStart(
                        request);

                    return;
                }

                if (value.Key ==
                    RequestStopEvent)
                {
                    var response =
                        GetProperty<HttpResponseMessage>(
                            value.Value,
                            "Response");

                    HandleStop(
                        request,
                        response);
                }
            }
            catch
            {
                // Telemetry instrumentation must never
                // interfere with the application's HTTP request.
            }
        }

        private static void HandleStart(
            HttpRequestMessage request)
        {
            if (TraceXBreadcrumbScope.Current is null)
            {
                return;
            }

            request.Options.Set(
                StartTimestampKey,
                Stopwatch.GetTimestamp());
        }

        private static void HandleStop(
            HttpRequestMessage request,
            HttpResponseMessage? response)
        {
            if (!request.Options.TryGetValue(
                    StartTimestampKey,
                    out var startTimestamp))
            {
                return;
            }

            var buffer =
                TraceXBreadcrumbScope.Current;

            if (buffer is null)
            {
                return;
            }

            var elapsed =
                Stopwatch.GetElapsedTime(
                    startTimestamp);

            var url =
                GetSanitizedUrl(
                    request.RequestUri);

            var statusCode =
                response is null
                    ? "0"
                    : ((int)response.StatusCode)
                        .ToString();

            buffer.Add(
                new TraceXBreadcrumb
                {
                    Timestamp =
                        DateTime.UtcNow,

                    Type =
                        "http.dependency",

                    Message =
                        $"{request.Method.Method} {url}",

                    Data =
                        new Dictionary<string, string>
                        {
                            ["method"] =
                                request.Method.Method,

                            ["url"] =
                                url,

                            ["status_code"] =
                                statusCode,

                            ["duration_ms"] =
                                elapsed.TotalMilliseconds
                                    .ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                        }
                });
        }

        private static string GetSanitizedUrl(
            Uri? uri)
        {
            if (uri is null)
            {
                return string.Empty;
            }

            if (!uri.IsAbsoluteUri)
            {
                return TraceXDataSanitizer.Sanitize(
                    uri.OriginalString);
            }

            var url =
                uri.GetLeftPart(
                    UriPartial.Authority) +
                uri.PathAndQuery;

            return TraceXDataSanitizer.Sanitize(
                url);
        }

        private static T? GetProperty<T>(
            object value,
            string propertyName)
            where T : class
        {
            return value
                .GetType()
                .GetProperty(propertyName)
                ?.GetValue(value) as T;
        }

        public void OnError(
            Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}