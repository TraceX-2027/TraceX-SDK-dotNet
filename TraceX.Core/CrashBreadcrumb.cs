using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TraceX.Core;

public sealed class CrashBreadcrumb
{
    [JsonPropertyName("sequence_order")]
    public int SequenceOrder { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; init; } = new();
}