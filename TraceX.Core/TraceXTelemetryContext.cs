using System.Reflection;
using System.Runtime.InteropServices;

namespace TraceX.Core;

public sealed class TraceXTelemetryContext
{
    private const int MaxEnvironmentFieldLength = 50;

    public string AppVersion { get; init; } =
        GetAppVersion();

    public string DeviceModel { get; init; } =
        GetDeviceModel();

    public string RuntimeVersion { get; init; } =
        Environment.Version.ToString();

    public string OsName { get; init; } =
        Clamp(
            RuntimeInformation.OSDescription,
            MaxEnvironmentFieldLength);

    public string OsVersion { get; init; } =
        Environment.OSVersion.Version.ToString();

    public string Hostname { get; init; } =
        Environment.MachineName;

    public long ProcessMemoryBytes { get; init; } =
        GC.GetTotalMemory(false);

    public string EnvironmentName { get; init; } =
        "Production";

    public static TraceXTelemetryContext Create(
        TraceXOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new TraceXTelemetryContext
        {
            AppVersion =
                GetAppVersion(),

            DeviceModel =
                GetDeviceModel(),

            RuntimeVersion =
                Environment.Version.ToString(),

            OsName =
                Clamp(
                    RuntimeInformation.OSDescription,
                    MaxEnvironmentFieldLength),

            OsVersion =
                Environment.OSVersion.Version.ToString(),

            Hostname =
                Environment.MachineName,

            ProcessMemoryBytes =
                GC.GetTotalMemory(false),

            EnvironmentName =
                Clamp(
                    options.Environment,
                    MaxEnvironmentFieldLength)
        };
    }

    private static string GetAppVersion()
    {
        return Assembly
            .GetEntryAssembly()?
            .GetName()
            .Version?
            .ToString()
            ?? "unknown";
    }

    private static string GetDeviceModel()
    {
        return Environment.MachineName;
    }

    private static string Clamp(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}