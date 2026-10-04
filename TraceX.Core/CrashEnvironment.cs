using System.Text.Json.Serialization;

namespace TraceX.Core;

public sealed class CrashEnvironment
{
    [JsonPropertyName("runtime_version")]
    public string RuntimeVersion { get; init; } = string.Empty;

    [JsonPropertyName("os_name")]
    public string OsName { get; init; } = string.Empty;

    [JsonPropertyName("os_version")]
    public string OsVersion { get; init; } = string.Empty;

    [JsonPropertyName("hostname")]
    public string Hostname { get; init; } = string.Empty;

    [JsonPropertyName("process_memory_bytes")]
    public long ProcessMemoryBytes { get; init; }

    [JsonPropertyName("environment_name")]
    public string EnvironmentName { get; init; } = string.Empty;
}