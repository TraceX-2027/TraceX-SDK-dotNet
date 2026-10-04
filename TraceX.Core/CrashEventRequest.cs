using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TraceX.Core;

public sealed class CrashEventRequest
{
    [JsonPropertyName("occurred_at")]
    public DateTime OccurredAt { get; init; }

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "dotnet";

    [JsonPropertyName("language")]
    public string Language { get; init; } = "csharp";

    [JsonPropertyName("exception_type")]
    public string ExceptionType { get; init; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; init; } = string.Empty;

    [JsonPropertyName("stack_trace")]
    public string StackTrace { get; init; } = string.Empty;

    [JsonPropertyName("environment")]
    public CrashEnvironment Environment { get; init; } = new();

    [JsonPropertyName("breadcrumbs")]
    public List<CrashBreadcrumb> Breadcrumbs { get; init; } = [];
}