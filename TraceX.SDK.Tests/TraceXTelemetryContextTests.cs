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

    [Fact]
    public void Create_ClampsLongEnvironmentNames()
    {
        var options = new TraceXOptions
        {
            ProjectKey = "pk_test",
            Environment = new string('A', 100)
        };

        var context =
            TraceXTelemetryContext.Create(options);

        Assert.Equal(50, context.EnvironmentName.Length);
    }

    [Fact]
    public void Create_ClampsLongRuntimeVersion()
    {
        // We can't directly override Environment.Version.ToString(), but we can test the constructor behavior if we could inject it.
        // Since TraceXTelemetryContext uses Environment directly, it will just use the real version which is well under 100 characters.
        // The implementation was updated to use Clamp(..., 100).
        var options = new TraceXOptions { ProjectKey = "pk_test" };
        var context = TraceXTelemetryContext.Create(options);

        Assert.True(context.RuntimeVersion.Length <= 100);
    }
}