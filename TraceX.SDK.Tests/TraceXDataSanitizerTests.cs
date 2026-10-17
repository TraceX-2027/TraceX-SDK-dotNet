using TraceX.Core;

namespace TraceX.SDK.Tests;

public sealed class TraceXDataSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsBearerToken()
    {
        var input =
            "Authorization: Bearer abc123-secret-token";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            "Authorization: Bearer [REDACTED]",
            result);
    }

    [Fact]
    public void Sanitize_RedactsStandaloneBearerToken()
    {
        var input =
            "Bearer abc123-secret-token";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            "Bearer [REDACTED]",
            result);
    }

    [Fact]
    public void Sanitize_RedactsApiKey()
    {
        var input =
            "api_key=super-secret-api-key";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            "api_key=[REDACTED]",
            result);
    }

    [Fact]
    public void Sanitize_RedactsConnectionStringPassword()
    {
        var input =
            "Server=db.example.com;Database=TraceX;Password=secret123;";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            "Server=db.example.com;Database=TraceX;Password=[REDACTED];",
            result);
    }

    [Fact]
    public void Sanitize_RedactsSensitiveQueryField()
    {
        var input =
            "/login?username=youssef&password=my-secret-password";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            "/login?username=youssef&password=[REDACTED]",
            result);
    }

    [Fact]
    public void Sanitize_PreservesNonSensitiveData()
    {
        var input =
            "method=GET&route=/api/users&status=200";

        var result =
            TraceXDataSanitizer.Sanitize(input);

        Assert.Equal(
            input,
            result);
    }

    [Fact]
    public void Sanitize_ReturnsEmptyForNull()
    {
        var result =
            TraceXDataSanitizer.Sanitize(null);

        Assert.Equal(
            string.Empty,
            result);
    }
}