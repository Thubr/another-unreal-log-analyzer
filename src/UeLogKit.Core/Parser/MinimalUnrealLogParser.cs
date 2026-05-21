using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UeLogKit.Core.Parser;

/// <summary>
/// Minimal Unreal log parser that supports only a narrow set of synthetic/core line formats.
/// Supported patterns:
/// 1) [yyyy.MM.dd-HH.mm.ss:fff][frame]Category: Verbosity: Message
/// 2) Category: Verbosity: Message
/// 3) Plain message fallback.
/// Intentionally out-of-scope for this minimal implementation:
/// locale-specific timestamps and advanced token extraction.
/// </summary>
public sealed class MinimalUnrealLogParser : ILogEventSource
{
    public async IAsyncEnumerable<LogEvent> ReadEventsAsync(
        LogInput input,
        ParserOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LogEvent? pending = null;
        var lineNumber = 0;

        await foreach (var rawLine in ReadLinesAsync(input.SourcePath, cancellationToken))
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (pending is not null && IsContinuation(rawLine))
            {
                if (options.IncludeContinuationLines)
                {
                    var updated = pending.ContinuationLines.Concat([rawLine.Trim()]).ToArray();
                    pending = pending with { ContinuationLines = updated };
                }

                continue;
            }

            if (pending is not null)
            {
                yield return pending;
            }

            pending = ParseLine(rawLine, lineNumber, input, options);
            await Task.Yield();
        }

        if (pending is not null)
        {
            yield return pending;
        }
    }

    private static bool IsContinuation(string line)
    {
        return line.Length > 0
            && char.IsWhiteSpace(line[0])
            && !LooksLikeLogEvent(line.TrimStart());
    }

    private static LogEvent ParseLine(string rawLine, int lineNumber, LogInput input, ParserOptions options)
    {
        var timestamp = default(DateTimeOffset?);
        var frame = default(int?);
        var category = "Unknown";
        var verbosity = "Display";
        var message = rawLine.Trim();

        var line = rawLine.TrimStart();
        var remainder = line;
        if (TryParseTimestampAndFrame(line, out var parsedTimestamp, out var parsedFrame, out var rest))
        {
            timestamp = parsedTimestamp;
            frame = parsedFrame;
            remainder = rest;
        }

        if (TryParseCategoryVerbosityMessage(remainder, out var parsedCategory, out var parsedVerbosity, out var parsedMessage))
        {
            category = parsedCategory;
            verbosity = parsedVerbosity;
            message = parsedMessage;
        }
        else if (TryParseCategoryMessage(remainder, out parsedCategory, out parsedMessage))
        {
            category = parsedCategory;
            message = parsedMessage;
        }

        var schemaVersion = options.StrictSchemaVersion ? options.ExpectedSchemaVersion : LogEventSchemaVersion.V1;
        var fields = ExtractFields(message);
        var hash = options.CaptureRawTextHash ? ComputeSha256(rawLine) : string.Empty;

        return new LogEvent(
            SchemaVersion: schemaVersion,
            SourceId: input.SourceId,
            SourcePath: input.SourcePath,
            LineNumber: lineNumber,
            Timestamp: timestamp,
            RelativeTime: null,
            Frame: frame,
            Category: category,
            Verbosity: verbosity,
            Message: message,
            ContinuationLines: Array.Empty<string>(),
            Fields: fields,
            RawTextHash: hash);
    }

    private static bool LooksLikeLogEvent(string line)
    {
        if (TryParseTimestampAndFrame(line, out _, out _, out var rest))
        {
            return TryParseCategoryVerbosityMessage(rest, out _, out _, out _)
                || TryParseCategoryMessage(rest, out _, out _);
        }

        return TryParseCategoryVerbosityMessage(line, out _, out _, out _)
            || TryParseCategoryMessage(line, out _, out _);
    }

    private static bool TryParseTimestampAndFrame(string line, out DateTimeOffset timestamp, out int frame, out string remainder)
    {
        timestamp = default;
        frame = default;
        remainder = line;

        if (!line.StartsWith('['))
        {
            return false;
        }

        var closeTimestamp = line.IndexOf(']');
        if (closeTimestamp < 0)
        {
            return false;
        }

        var timestampText = line[1..closeTimestamp];
        const string format = "yyyy.MM.dd-HH.mm.ss:fff";
        if (!DateTimeOffset.TryParseExact(timestampText, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp))
        {
            return false;
        }

        var frameStart = line.IndexOf('[', closeTimestamp + 1);
        var frameEnd = line.IndexOf(']', frameStart + 1);
        if (frameStart < 0 || frameEnd < 0)
        {
            return false;
        }

        if (!int.TryParse(line[(frameStart + 1)..frameEnd].Trim(), out frame))
        {
            return false;
        }

        remainder = line[(frameEnd + 1)..].TrimStart();
        return true;
    }

    private static bool TryParseCategoryVerbosityMessage(string line, out string category, out string verbosity, out string message)
    {
        category = "Unknown";
        verbosity = "Display";
        message = line.Trim();

        var firstColon = line.IndexOf(':');
        if (firstColon <= 0)
        {
            return false;
        }

        var secondColon = line.IndexOf(':', firstColon + 1);
        if (secondColon <= firstColon + 1)
        {
            return false;
        }

        category = line[..firstColon].Trim();
        verbosity = line[(firstColon + 1)..secondColon].Trim();
        message = line[(secondColon + 1)..].Trim();
        return IsLogCategory(category) && IsKnownVerbosity(verbosity);
    }

    private static bool TryParseCategoryMessage(string line, out string category, out string message)
    {
        category = "Unknown";
        message = line.Trim();

        var firstColon = line.IndexOf(':');
        if (firstColon <= 0)
        {
            return false;
        }

        category = line[..firstColon].Trim();
        message = line[(firstColon + 1)..].Trim();
        return IsLogCategory(category) && message.Length > 0;
    }

    private static bool IsLogCategory(string category)
    {
        return category.StartsWith("Log", StringComparison.Ordinal)
            && category.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static bool IsKnownVerbosity(string verbosity)
    {
        return verbosity.Equals("Fatal", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("Warning", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("Display", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("Log", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("Verbose", StringComparison.OrdinalIgnoreCase)
            || verbosity.Equals("VeryVerbose", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> ExtractFields(string message)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index < message.Length)
        {
            while (index < message.Length && char.IsWhiteSpace(message[index]))
            {
                index++;
            }

            var tokenStart = index;
            var separator = message.IndexOf('=', tokenStart);
            if (separator < 0)
            {
                break;
            }

            if (separator == tokenStart || message[tokenStart..separator].Any(char.IsWhiteSpace))
            {
                index = NextTokenIndex(message, tokenStart);
                continue;
            }

            var key = message[tokenStart..separator];
            var valueStart = separator + 1;
            if (valueStart >= message.Length)
            {
                index = valueStart;
                continue;
            }

            string value;
            if (message[valueStart] == '"')
            {
                valueStart++;
                var valueEnd = message.IndexOf('"', valueStart);
                valueEnd = valueEnd < 0 ? message.Length : valueEnd;
                value = message[valueStart..valueEnd];
                index = valueEnd < message.Length ? valueEnd + 1 : valueEnd;
            }
            else
            {
                var valueEnd = NextTokenIndex(message, valueStart);
                value = message[valueStart..valueEnd].TrimEnd(',', ';');
                index = valueEnd;
            }

            fields[key] = value;
        }

        return fields;
    }

    private static int NextTokenIndex(string message, int start)
    {
        var index = start;
        while (index < message.Length && !char.IsWhiteSpace(message[index]))
        {
            index++;
        }

        return index;
    }

    private static string ComputeSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string sourcePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(sourcePath);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is not null)
            {
                yield return line;
            }
        }
    }
}
