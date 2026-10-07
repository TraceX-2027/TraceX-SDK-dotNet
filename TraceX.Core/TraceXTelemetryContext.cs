using System.Reflection;
using System.Runtime.InteropServices;

namespace TraceX.Core;

public sealed class TraceXTelemetryContext
{
    private const int MaxEnvironmentFieldLength = 50;

    public string AppVersion { get; init; } =
        Clamp(
            GetAppVersion(),
            50);

    public string DeviceModel { get; init; } =
        Clamp(
            GetDeviceModel(),
            100);

    public string RuntimeVersion { get; init; } =
        Clamp(
            Environment.Version.ToString(),
            100);

    public string OsName { get; init; } =
        Clamp(
            RuntimeInformation.OSDescription,
            MaxEnvironmentFieldLength);

    public string OsVersion { get; init; } =
        Clamp(
            Environment.OSVersion.Version.ToString(),
            50);

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
                Clamp(
                    GetAppVersion(),
                    50),

            DeviceModel =
                Clamp(
                    GetDeviceModel(),
                    100),

            RuntimeVersion =
                Clamp(
                    Environment.Version.ToString(),
                    100),

            OsName =
                Clamp(
                    RuntimeInformation.OSDescription,
                    MaxEnvironmentFieldLength),

            OsVersion =
                Clamp(
                    Environment.OSVersion.Version.ToString(),
                    50),

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