using System.Runtime.InteropServices;
using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXTelemetryContextTests
{
    [Fact]
    public void Create_PopulatesRuntimeAndOperatingSystemInformation()
    {
        var options = new TraceXOptions
        {
            ProjectKey = "pk_test",
            Environment = "Development"
        };

        var context =
            TraceXTelemetryContext.Create(options);

        Assert.Equal(
            Environment.Version.ToString(),
            context.RuntimeVersion);

        Assert.Equal(
            RuntimeInformation.OSDescription,
            context.OsName);

        Assert.Equal(
            Environment.OSVersion.Version.ToString(),
            context.OsVersion);
    }

    [Fact]
    public void Create_PopulatesMachineAndMemoryInformation()
    {
        var options = new TraceXOptions
        {
            ProjectKey = "pk_test",
            Environment = "Development"
        };

        var context =
            TraceXTelemetryContext.Create(options);

        Assert.Equal(
            Environment.MachineName,
            context.Hostname);

        Assert.True(
            context.ProcessMemoryBytes > 0);
    }

    [Fact]
    public void Create_UsesConfiguredEnvironment()
    {
        var options = new TraceXOptions
        {
            ProjectKey = "pk_test",
            Environment = "Staging"
        };

        var context =
            TraceXTelemetryContext.Create(options);

        Assert.Equal(
            "Staging",
            context.EnvironmentName);
    }
}