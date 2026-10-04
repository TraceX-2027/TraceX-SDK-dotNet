using System.Reflection;
using System.Runtime.InteropServices;

namespace TraceX.Core;

public sealed class TraceXTelemetryContext
{
    public string AppVersion { get; init; } =
        GetAppVersion();

    public string DeviceModel { get; init; } =
        GetDeviceModel();

    public string RuntimeVersion { get; init; } =
        Environment.Version.ToString();

    public string OsName { get; init; } =
        RuntimeInformation.OSDescription;

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
                RuntimeInformation.OSDescription,

            OsVersion =
                Environment.OSVersion.Version.ToString(),

            Hostname =
                Environment.MachineName,

            ProcessMemoryBytes =
                GC.GetTotalMemory(false),

            EnvironmentName =
                options.Environment
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
}