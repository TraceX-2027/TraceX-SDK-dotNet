using System.Threading;

namespace TraceX.Core;

public static class TraceXBreadcrumbScope
{
    private static readonly AsyncLocal<BreadcrumbBuffer?> CurrentBuffer =
        new();

    public static BreadcrumbBuffer? Current
    {
        get => CurrentBuffer.Value;
        set => CurrentBuffer.Value = value;
    }
}