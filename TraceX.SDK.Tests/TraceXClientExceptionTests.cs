using TraceX.Core;
using Xunit;

namespace TraceX.SDK.Tests;

public sealed class TraceXClientExceptionTests
{
    private class VeryLongExceptionName01234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789 : Exception { }

    [Fact]
    public void CaptureException_ClampsLongExceptionType()
    {
        var options = new TraceXOptions { ProjectKey = "pk_test" };
        using var client = new TraceXClient(options);

        CrashEventRequest? request = null;
        client.RequestEnqueued += r => request = r;

        var ex = new VeryLongExceptionName01234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789();
        
        client.CaptureException(ex);

        Assert.NotNull(request);
        Assert.Equal(150, request!.ExceptionType.Length);
        Assert.StartsWith("TraceX.SDK.Tests.TraceXClientExceptionTests+VeryLongException", request.ExceptionType);
    }
}
