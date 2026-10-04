using System.Text.RegularExpressions;

namespace TraceX.Core;

public static partial class TraceXDataSanitizer
{
    private const string RedactedValue = "[REDACTED]";

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value;

        sanitized = BearerTokenRegex().Replace(
            sanitized,
            $"$1{RedactedValue}");

        sanitized = ConnectionStringSecretRegex().Replace(
            sanitized,
            $"$1{RedactedValue}");

        sanitized = ApiKeyRegex().Replace(
            sanitized,
            $"$1{RedactedValue}");

        sanitized = SensitiveFieldRegex().Replace(
            sanitized,
            $"$1$2{RedactedValue}");

        return sanitized;
    }

    [GeneratedRegex(
        @"(?i)(Authorization\s*:\s*Bearer\s+)[^\s,;]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(
        @"(?i)\b((?:Password|Pwd|User Password|Passphrase)\s*=\s*)[^;,]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringSecretRegex();

    [GeneratedRegex(
        @"(?i)\b((?:api[_-]?key|x-api-key|apikey|access[_-]?key)\s*[:=]\s*)[^\s&,;]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(
        @"(?i)(^|[?&;\s])((?:password|passwd|pwd|token|access_token|refresh_token|secret|client_secret|authorization)\s*=\s*)[^&;\s]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveFieldRegex();
}