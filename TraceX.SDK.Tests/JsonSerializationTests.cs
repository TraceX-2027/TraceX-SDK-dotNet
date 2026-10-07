using System.Text.Json;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class JsonSerializationTests
{
    [Fact]
    public void CrashEventRequest_SerializesToExactApiSpecification()
    {
        // Arrange
        var request = new CrashEventRequest
        {
            ProjectKey = "tracex_live_pub_98a72bc4",
            Platform = "dotnet",
            Language = "csharp",
            OccurredAt = new DateTime(2026, 9, 2, 10, 15, 30, 124, DateTimeKind.Utc),
            ExceptionType = "NullReferenceException",
            ErrorMessage = "Object reference not set to an instance of an object.",
            StackTrace = "at MyNamespace.MyClass.MyMethod() in C:\\src\\file.cs:line 42",
            Environment = new CrashEnvironment
            {
                AppVersion = "1.0.4+12",
                RuntimeVersion = ".NET 9.0.0",
                OsName = "Windows",
                OsVersion = "10.0.19045",
                DeviceModel = "PC",
                Hostname = "MY-PC",
                ProcessMemoryBytes = 142000000,
                EnvironmentName = "Production"
            },
            Breadcrumbs = new List<CrashBreadcrumb>
            {
                new()
                {
                    SequenceOrder = 1,
                    Timestamp = new DateTime(2026, 9, 2, 10, 15, 20, 0, DateTimeKind.Utc),
                    Category = "network.http",
                    Action = "GET",
                    Target = "/api/users",
                    Data = new Dictionary<string, string>
                    {
                        { "status_code", "200" }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("project_key", out var projectKey));
        Assert.Equal("tracex_live_pub_98a72bc4", projectKey.GetString());

        Assert.True(root.TryGetProperty("platform", out var platform));
        Assert.Equal("dotnet", platform.GetString());

        Assert.True(root.TryGetProperty("language", out var language));
        Assert.Equal("csharp", language.GetString());

        Assert.True(root.TryGetProperty("occurred_at", out var occurredAt));
        Assert.Equal("2026-09-02T10:15:30.124Z", occurredAt.GetString());

        Assert.True(root.TryGetProperty("exception_type", out var exceptionType));
        Assert.Equal("NullReferenceException", exceptionType.GetString());

        Assert.True(root.TryGetProperty("error_message", out var errorMessage));
        Assert.Equal("Object reference not set to an instance of an object.", errorMessage.GetString());

        Assert.True(root.TryGetProperty("stack_trace", out var stackTrace));
        Assert.Equal("at MyNamespace.MyClass.MyMethod() in C:\\src\\file.cs:line 42", stackTrace.GetString());

        Assert.True(root.TryGetProperty("environment", out var environment));
        Assert.Equal("1.0.4+12", environment.GetProperty("app_version").GetString());
        Assert.Equal(".NET 9.0.0", environment.GetProperty("runtime_version").GetString());
        Assert.Equal("Windows", environment.GetProperty("os_name").GetString());
        Assert.Equal("10.0.19045", environment.GetProperty("os_version").GetString());
        Assert.Equal("PC", environment.GetProperty("device_model").GetString());
        Assert.Equal("MY-PC", environment.GetProperty("hostname").GetString());
        Assert.Equal(142000000, environment.GetProperty("process_memory_bytes").GetInt64());
        Assert.Equal("Production", environment.GetProperty("environment_name").GetString());

        Assert.True(root.TryGetProperty("breadcrumbs", out var breadcrumbs));
        Assert.Equal(1, breadcrumbs.GetArrayLength());
        
        var firstBreadcrumb = breadcrumbs[0];
        Assert.Equal(1, firstBreadcrumb.GetProperty("sequence_order").GetInt32());
        Assert.Equal("2026-09-02T10:15:20Z", firstBreadcrumb.GetProperty("timestamp").GetString());
        Assert.Equal("network.http", firstBreadcrumb.GetProperty("category").GetString());
        Assert.Equal("GET", firstBreadcrumb.GetProperty("action").GetString());
        Assert.Equal("/api/users", firstBreadcrumb.GetProperty("target").GetString());
        Assert.Equal("200", firstBreadcrumb.GetProperty("data").GetProperty("status_code").GetString());
    }
}
