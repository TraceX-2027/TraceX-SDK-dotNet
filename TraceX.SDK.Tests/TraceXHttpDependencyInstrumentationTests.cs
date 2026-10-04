using System.Net;
using System.Net.Sockets;
using System.Text;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXHttpDependencyInstrumentationTests
{
    [Fact]
    public async Task HttpClientRequest_CapturesDependencyBreadcrumb()
    {
        TraceXHttpDependencyInstrumentation.EnsureStarted();

        var buffer = new BreadcrumbBuffer();

        TraceXBreadcrumbScope.Current = buffer;

        try
        {
            using var server =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            server.Start();

            var endpoint =
                (IPEndPoint)server.LocalEndpoint;

            var serverTask =
                AcceptHttpRequestAsync(server);

            using var httpClient =
                new HttpClient();

            var response =
                await httpClient.GetAsync(
                    $"http://127.0.0.1:{endpoint.Port}/api/test?page=2");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            await serverTask;

            var breadcrumbs =
                buffer.GetAll();

            var breadcrumb =
                Assert.Single(
                    breadcrumbs,
                    item =>
                        item.Type ==
                        "http.dependency");

            Assert.Equal(
                "http.dependency",
                breadcrumb.Type);

            Assert.Equal(
                "GET",
                breadcrumb.Data["method"]);

            Assert.Contains(
                "/api/test?page=2",
                breadcrumb.Data["url"]);

            Assert.Equal(
                "200",
                breadcrumb.Data["status_code"]);

            Assert.True(
                double.TryParse(
                    breadcrumb.Data["duration_ms"],
                    out var duration));

            Assert.True(
                duration >= 0);
        }
        finally
        {
            TraceXBreadcrumbScope.Current = null;
        }
    }

    [Fact]
    public async Task HttpClientRequest_SanitizesSensitiveQueryParameters()
    {
        TraceXHttpDependencyInstrumentation.EnsureStarted();

        var buffer = new BreadcrumbBuffer();

        TraceXBreadcrumbScope.Current = buffer;

        try
        {
            using var server =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            server.Start();

            var endpoint =
                (IPEndPoint)server.LocalEndpoint;

            var serverTask =
                AcceptHttpRequestAsync(server);

            using var httpClient =
                new HttpClient();

            var response =
                await httpClient.GetAsync(
                    $"http://127.0.0.1:{endpoint.Port}" +
                    "/login?username=youssef&password=secret123");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            await serverTask;

            var breadcrumbs =
                buffer.GetAll();

            var breadcrumb =
                Assert.Single(
                    breadcrumbs,
                    item =>
                        item.Type ==
                        "http.dependency");

            Assert.Contains(
                "username=youssef",
                breadcrumb.Data["url"]);

            Assert.Contains(
                "password=[REDACTED]",
                breadcrumb.Data["url"]);

            Assert.DoesNotContain(
                "secret123",
                breadcrumb.Data["url"]);
        }
        finally
        {
            TraceXBreadcrumbScope.Current = null;
        }
    }

    private static async Task AcceptHttpRequestAsync(
        TcpListener server)
    {
        using var client =
            await server.AcceptTcpClientAsync();

        using var stream =
            client.GetStream();

        var requestBuffer =
            new byte[4096];

        var bytesRead =
            await stream.ReadAsync(
                requestBuffer);

        _ = bytesRead;

        var response =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Length: 2\r\n" +
            "Connection: close\r\n" +
            "\r\n" +
            "OK";

        var responseBytes =
            Encoding.ASCII.GetBytes(response);

        await stream.WriteAsync(
            responseBytes);
    }
}