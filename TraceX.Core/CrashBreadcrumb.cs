using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TraceX.Core;

public sealed class CrashBreadcrumb
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; init; } = new();
}