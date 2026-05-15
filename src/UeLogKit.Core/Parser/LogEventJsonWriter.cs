using System.Text.Json;

namespace UeLogKit.Core.Parser;

public sealed class LogEventJsonWriter : ILogEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task WriteJsonArrayAsync(IReadOnlyList<LogEvent> events, TextWriter writer, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(events, JsonOptions);
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
    }

    public async Task WriteNdjsonAsync(IAsyncEnumerable<LogEvent> events, TextWriter writer, CancellationToken cancellationToken = default)
    {
        await foreach (var e in events.WithCancellation(cancellationToken))
        {
            var line = JsonSerializer.Serialize(e);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}
