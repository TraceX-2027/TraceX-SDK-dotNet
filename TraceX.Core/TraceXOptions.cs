namespace TraceX.Core;

public sealed class TraceXOptions
{
    public string ProjectKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } =
        "https://tracex-api.kareemadel.com/api/v1/";

    public bool EnableLogging { get; set; } = false;

    public string Environment { get; set; } = "Production";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectKey))
        {
            throw new InvalidOperationException(
                "TraceX ProjectKey is required. Configure a valid ProjectKey before starting the application.");
        }

        if (!ProjectKey.StartsWith(
                "tracex_live_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TraceX ProjectKey is malformed. It must start with 'tracex_live_'.");
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException(
                "TraceX Endpoint cannot be empty.");
        }
    }
}